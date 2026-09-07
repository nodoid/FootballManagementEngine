using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class UkDatabaseTests
{
    private string _databasePath = null!;

    [SetUp]
    public void SetUp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fme-tests");
        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, $"{Guid.NewGuid():N}.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { /* harmless temp file */ }
    }

    private FootballGameEngine NewGame(bool loadExisting = false, string slot = "default") =>
        UkDatabase.Create(_databasePath, loadExisting, autoSave: false, slot);

    [Test]
    public void Create_BuildsTheFiveEnglishTiers()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues.Keys, Is.EquivalentTo(new[] { "PL", "CH", "L1", "L2", "NL" }));
            Assert.That(game.State.Leagues["PL"].Level, Is.EqualTo(1));
            Assert.That(game.State.Leagues["NL"].Level, Is.EqualTo(5));
            Assert.That(game.State.Leagues.Values.Select(l => l.Level), Is.Unique);
        });
    }

    [Test]
    public void Create_SetsPromotionRelegationAndPlayoffSpots()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues["PL"].RelegationSpots, Is.EqualTo(3));
            Assert.That(game.State.Leagues["PL"].PlayoffSpots, Is.EqualTo(4));
            Assert.That(game.State.Leagues["CH"].PromotionSpots, Is.EqualTo(3));
            Assert.That(game.State.Leagues["CH"].RelegationSpots, Is.EqualTo(3));
            Assert.That(game.State.Leagues["L1"].PromotionSpots, Is.EqualTo(3));
            Assert.That(game.State.Leagues["L1"].RelegationSpots, Is.EqualTo(4));
            Assert.That(game.State.Leagues["L2"].PromotionSpots, Is.EqualTo(4));
            Assert.That(game.State.Leagues["L2"].RelegationSpots, Is.EqualTo(2));
            Assert.That(game.State.Leagues["NL"].PromotionSpots, Is.EqualTo(2));
            Assert.That(game.State.Leagues["NL"].PlayoffSpots, Is.EqualTo(2));
        });
    }

    [Test]
    public void Create_PairsEachTiersRelegationPlacesWithThePromotionPlacesBelow()
    {
        var game = NewGame();
        var tiers = game.State.Leagues.Values.OrderBy(l => l.Level).ToList();

        for (var i = 0; i < tiers.Count - 1; i++)
        {
            Assert.That(tiers[i + 1].PromotionSpots, Is.EqualTo(tiers[i].RelegationSpots),
                $"{tiers[i].Id} -> {tiers[i + 1].Id} would change both divisions' size");
        }
    }

    [Test]
    public void Create_FillsEveryDivision()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Teams, Has.Count.EqualTo(116));
            Assert.That(game.State.Leagues["PL"].TeamIds, Has.Count.EqualTo(20));
            Assert.That(game.State.Leagues["CH"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["L1"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["L2"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["NL"].TeamIds, Has.Count.EqualTo(24));
        });
    }

    [Test]
    public void Create_GivesEveryTeamAnIdentityAndALeague()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Teams.Values.Select(t => t.Name), Is.All.Not.Empty);
            Assert.That(game.State.Teams.Values.Select(t => t.ShortName), Is.All.Not.Empty);
            Assert.That(game.State.Teams.Values.Select(t => t.LeagueId), Is.All.Not.Empty);
            Assert.That(game.State.Teams.Values.All(t => game.State.Leagues.ContainsKey(t.LeagueId)), Is.True);
            Assert.That(game.State.Teams.Keys, Is.Unique);
        });
    }

    [Test]
    public void Create_EveryLeagueMembershipAgreesWithTheTeamRecord()
    {
        var game = NewGame();

        foreach (var league in game.State.Leagues.Values)
            foreach (var teamId in league.TeamIds)
                Assert.That(game.State.Teams[teamId].LeagueId, Is.EqualTo(league.Id), teamId);

        Assert.That(game.State.Leagues.Values.SelectMany(l => l.TeamIds), Is.Unique);
    }

    [Test]
    public void Create_GivesEveryClubATwentyTwoManSquad()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Teams.Values.Select(t => t.Players.Count), Is.All.EqualTo(22));
            Assert.That(game.State.Teams.Values.SelectMany(t => t.Players).Select(p => p.Id), Is.Unique);
        });
    }

    [Test]
    public void Create_BuildsABalancedSquadWithAGoalkeeper()
    {
        var squad = NewGame().State.Teams["ARS"].Players;

        Assert.Multiple(() =>
        {
            Assert.That(squad.Count(p => p.Position == Position.GK), Is.EqualTo(2),
                "a club needs a second keeper for cover and for the bench");
            Assert.That(squad.Count(p => p.Position == Position.DEF), Is.EqualTo(7));
            Assert.That(squad.Count(p => p.Position == Position.MID), Is.EqualTo(8));
            Assert.That(squad.Count(p => p.Position == Position.FWD), Is.EqualTo(5));
            Assert.That(squad.Where(p => p.Position == Position.GK).Select(p => p.Goalkeeping),
                Is.All.GreaterThan(50));
            Assert.That(squad.Where(p => p.Position != Position.GK).Select(p => p.Goalkeeping),
                Is.All.LessThan(50));
        });
    }

    [Test]
    public void Create_GivesPlayersPlausibleAttributes()
    {
        var players = NewGame().State.Teams.Values.SelectMany(t => t.Players).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(players.Select(p => p.Overall), Is.All.InRange(1, 99));
            Assert.That(players.Select(p => p.Age), Is.All.InRange(16, 45));
            Assert.That(players.Select(p => p.Potential), Is.All.LessThanOrEqualTo(95));
            Assert.That(players.All(p => p.Potential >= p.Overall), Is.True);
            Assert.That(players.Select(p => p.WeeklyWage), Is.All.GreaterThan(0));
            Assert.That(players.Select(p => p.ContractYears), Is.All.InRange(1, 4));
            Assert.That(players.Select(p => p.Injured), Is.All.False);
        });
    }

    [Test]
    public void Create_TiesEveryPlayerToTheirClub()
    {
        var game = NewGame();

        foreach (var team in game.State.Teams.Values)
            Assert.That(team.Players.Select(p => p.ContractClubId), Is.All.EqualTo(team.Id), team.Id);
    }

    [Test]
    public void Create_RanksReputationByDivision()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Teams["ARS"].Reputation, Is.EqualTo(70));
            Assert.That(game.State.Teams["LEI"].Reputation, Is.EqualTo(60));
            Assert.That(game.State.Teams["BAR"].Reputation, Is.EqualTo(50));
            Assert.That(game.State.Teams["GIL"].Reputation, Is.EqualTo(42));
            Assert.That(game.State.Teams["WOK"].Reputation, Is.EqualTo(35));
        });
    }

    [Test]
    public void Create_RegistersALeagueCompetitionForEveryDivision()
    {
        var game = NewGame();

        foreach (var league in game.State.Leagues.Values)
        {
            var competition = game.State.Competitions.Values
                .Single(c => c.Type == CompetitionType.League && c.LeagueId == league.Id);

            Assert.That(competition.TeamIds, Is.EquivalentTo(league.TeamIds), league.Id);
        }
    }

    [Test]
    public void Create_RegistersTheDomesticCups()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Competitions["FA"].Type, Is.EqualTo(CompetitionType.FaCup));
            Assert.That(game.State.Competitions["FA"].TeamIds, Has.Count.EqualTo(116));
            Assert.That(game.State.Competitions["CARABAO"].Type, Is.EqualTo(CompetitionType.LeagueCup));
            Assert.That(game.State.Competitions["CARABAO"].TeamIds, Has.Count.EqualTo(44));
            Assert.That(game.State.Competitions["EFLT"].Type, Is.EqualTo(CompetitionType.EflTrophy));
            Assert.That(game.State.Competitions["EFLT"].TeamIds, Has.Count.EqualTo(48));
        });
    }

    [Test]
    public void Create_OnlyTheFaCupAllowsReplays()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Competitions["FA"].MatchRules.ReplayAllowed, Is.True);
            Assert.That(game.State.Competitions["FA"].MatchRules.MaxReplays, Is.EqualTo(1));
            Assert.That(game.State.Competitions["CARABAO"].MatchRules.ReplayAllowed, Is.False);
            Assert.That(game.State.Competitions["EFLT"].MatchRules.ReplayAllowed, Is.False);
            Assert.That(game.State.Competitions["CARABAO"].MatchRules.PenaltiesAllowed, Is.True);
        });
    }

    [Test]
    public void Create_RegistersTheEuropeanCompetitionsWithoutEntrants()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            foreach (var id in new[] { "UCL", "UEL", "UECL" })
            {
                Assert.That(game.State.Competitions[id].Type, Is.EqualTo(CompetitionType.EuropeanLeaguePhase), id);
                Assert.That(game.State.Competitions[id].TeamIds, Is.Empty, id);
                Assert.That(game.State.Competitions[id].LeagueId, Is.Null, id);
            }
        });
    }

    [Test]
    public void Create_SeedsSeasonStatsForEveryPlayer()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.PlayerStats, Has.Count.EqualTo(116 * 22));
            Assert.That(game.State.PlayerStats.Values.Select(s => s.Appearances), Is.All.Zero);
            Assert.That(game.State.PlayerStats.Values.Select(s => s.Season), Is.All.EqualTo(2026));
        });
    }

    [Test]
    public void Create_StartsWithNoFixturesAndNoManager()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Fixtures, Is.Empty);
            Assert.That(game.State.PlayerTeamId, Is.Null);
            Assert.That(game.State.Season, Is.EqualTo(2026));
        });
    }

    [Test]
    public void Create_AttachesPersistence()
    {
        var game = NewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.Persistence, Is.Not.Null);
            Assert.That(File.Exists(_databasePath), Is.True);
        });
    }

    [Test]
    public void Create_IsDeterministicAcrossRuns()
    {
        var first = NewGame();
        var second = NewGame();

        Assert.That(second.ExportState(), Is.EqualTo(first.ExportState()));
    }

    [Test]
    public void Create_WithLoadExisting_ButNoSave_StartsANewGame()
    {
        var game = NewGame(loadExisting: true);

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Teams, Has.Count.EqualTo(116));
            Assert.That(game.State.PlayerTeamId, Is.Null);
        });
    }

    [Test]
    public void Create_WithLoadExisting_ResumesTheSavedGame()
    {
        var original = NewGame();
        original.SelectPlayerTeam("LIV");
        original.SetFormation("LIV", Formation.F433);
        original.Save("career");

        var resumed = UkDatabase.Create(_databasePath, loadExisting: true, autoSave: false, "career");

        Assert.Multiple(() =>
        {
            Assert.That(resumed.State.PlayerTeamId, Is.EqualTo("LIV"));
            Assert.That(resumed.State.Teams["LIV"].Formation, Is.EqualTo(Formation.F433));
            Assert.That(resumed.State.Teams, Has.Count.EqualTo(116));
        });
    }

    [Test]
    public void Create_WithLoadExisting_OnlyResumesTheRequestedSlot()
    {
        var original = NewGame();
        original.SelectPlayerTeam("EVE");
        original.Save("career");

        var resumed = UkDatabase.Create(_databasePath, loadExisting: true, autoSave: false, "other");

        Assert.That(resumed.State.PlayerTeamId, Is.Null, "an unsaved slot must start a fresh game");
    }

    [Test]
    public void Create_WithLoadExisting_HonoursTheAutoSaveFlag()
    {
        var original = NewGame();
        original.Save("career");

        var resumed = UkDatabase.Create(_databasePath, loadExisting: true, autoSave: true, "career");

        Assert.That(resumed.AutoSave, Is.True);
    }

    [Test]
    public void Create_Parameterless_UsesTheDefaultDatabaseFile()
    {
        var existed = File.Exists(GamePersistence.DefaultDatabasePath);
        try
        {
            var game = UkDatabase.Create();

            Assert.Multiple(() =>
            {
                Assert.That(game.State.Teams, Has.Count.EqualTo(116));
                Assert.That(game.AutoSave, Is.False);
                Assert.That(File.Exists(GamePersistence.DefaultDatabasePath), Is.True);
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (!existed)
            {
                try { File.Delete(GamePersistence.DefaultDatabasePath); }
                catch (IOException) { /* harmless */ }
            }
        }
    }

    [Test]
    public void Create_ProducesAWorldTheSeasonEngineCanSchedule()
    {
        var game = NewGame();
        var season = new SeasonEngine(game);

        season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            // 20*19 for the Premier League plus 24*23 for each of the four EFL divisions.
            Assert.That(game.State.Fixtures, Has.Count.EqualTo(380 + 4 * 552));
            Assert.That(game.State.Fixtures.Select(f => f.HomeTeamId), Is.All.Not.EqualTo("BYE"));
            foreach (var league in game.State.Leagues.Values)
            {
                var competitionId = game.State.Competitions.Values
                    .Single(c => c.Type == CompetitionType.League && c.LeagueId == league.Id).Id;
                var played = game.State.Fixtures.Count(f => f.CompetitionId == competitionId);
                Assert.That(played, Is.EqualTo(league.TeamIds.Count * (league.TeamIds.Count - 1)), league.Id);
            }
        });
    }

    [Test]
    public void Create_ProducesAWorldTheFaCupDrawCanUse()
    {
        var game = NewGame();

        new SeasonEngine(game).GenerateFaCup();

        var ties = game.State.Fixtures.Where(f => f.CompetitionId == "FA").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ties, Has.Count.EqualTo(58));
            Assert.That(ties.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }), Is.Unique);
            Assert.That(ties.Select(f => f.TieId), Is.Unique);
        });
    }
}
