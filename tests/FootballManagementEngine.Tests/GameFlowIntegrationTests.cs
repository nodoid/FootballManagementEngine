using System.Text.Json;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

/// <summary>
/// End-to-end coverage of the flows the console entry point performs: build a world, schedule the
/// season, play it, and resume from disk.
/// </summary>
[TestFixture]
public class GameFlowIntegrationTests
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

    /// <summary>Mirrors the new-game branch of Program.cs.</summary>
    private FootballGameEngine StartNewGame(string slot = "default")
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: true, slot);
        var season = new SeasonEngine(game);

        season.GenerateDomesticSeason();
        season.GenerateFaCup();
        game.State.Competitions["UCL"].TeamIds.AddRange(["ARS", "LIV", "MCI", "MUN", "CHE", "NEW"]);
        game.State.Competitions["UEL"].TeamIds.AddRange(["AVL", "TOT", "BHA", "WHU"]);
        game.State.Competitions["UECL"].TeamIds.AddRange(["CRY", "FUL"]);
        season.GenerateEuropeanFixtures();
        game.Save(slot);

        return game;
    }

    [Test]
    public void NewGameStartup_SchedulesEveryCompetition()
    {
        var game = StartNewGame();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId.EndsWith("-COMP")), Is.EqualTo(2588));
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId == "FA"), Is.EqualTo(58));
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId == "UCL"), Is.EqualTo(24));
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId == "UEL"), Is.EqualTo(16));
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId == "UECL"), Is.EqualTo(8));
            Assert.That(game.State.Fixtures.Select(f => f.Id), Is.Unique);
        });
    }

    [Test]
    public void NewGameStartup_EveryFixtureReferencesRealTeamsAndCompetitions()
    {
        var game = StartNewGame();

        foreach (var fixture in game.State.Fixtures)
        {
            Assert.That(game.State.Competitions, Does.ContainKey(fixture.CompetitionId));
            Assert.That(game.State.Teams, Does.ContainKey(fixture.HomeTeamId));
            Assert.That(game.State.Teams, Does.ContainKey(fixture.AwayTeamId));
            Assert.That(fixture.HomeTeamId, Is.Not.EqualTo(fixture.AwayTeamId));
        }
    }

    [Test]
    public void NewGameStartup_IsPersistedAndCanBeResumed()
    {
        var game = StartNewGame("career");
        var fixtureCount = game.State.Fixtures.Count;

        var resumed = UkDatabase.Create(_databasePath, loadExisting: true, autoSave: true, "career");

        Assert.Multiple(() =>
        {
            Assert.That(resumed.State.Fixtures, Has.Count.EqualTo(fixtureCount));
            Assert.That(resumed.State.Teams, Has.Count.EqualTo(116));
            Assert.That(resumed.State.Competitions["UCL"].TeamIds, Has.Count.EqualTo(6));
        });
    }

    [Test]
    public void NewGameStartup_TheTeamsEndpointListsEveryClub()
    {
        var api = new GameApi(StartNewGame());

        var response = api.Handle("GET", GameApi.TeamsPath);
        var teams = JsonDocument.Parse(response.Body).RootElement.GetProperty("teams");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(teams.GetArrayLength(), Is.EqualTo(116));
        });
    }

    [Test]
    public void ManagerJourney_SelectClubSetTacticsPlayAMatchAndCheckTheTable()
    {
        var game = StartNewGame();
        var api = new GameApi(game);

        Assert.That(api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":"ARS"}""").StatusCode, Is.EqualTo(200));
        Assert.That(api.Handle("POST", GameApi.FormationPath, """{"teamId":"ARS","formation":"F433"}""").StatusCode,
            Is.EqualTo(200));

        var nextMatch = game.Fixtures(competitionId: "PL-COMP", teamId: "ARS").First(f => !f.IsPlayed);
        var simulate = api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{nextMatch.Id}}","seed":123}""");
        var result = JsonDocument.Parse(simulate.Body).RootElement.GetProperty("result");

        var table = game.GetLeagueTable("PL");
        var arsenal = table.Single(r => r.TeamId == "ARS");

        Assert.Multiple(() =>
        {
            Assert.That(simulate.StatusCode, Is.EqualTo(200));
            Assert.That(result.GetProperty("highlights").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(arsenal.Played, Is.EqualTo(1));
            Assert.That(table.Sum(r => r.Played), Is.EqualTo(2));
            Assert.That(game.State.PlayerStats["ARS-P01"].Appearances, Is.EqualTo(1));
        });
    }

    [Test]
    public void ManagerJourney_ProgressIsAutoSavedAfterEveryStep()
    {
        var game = StartNewGame();
        var api = new GameApi(game);
        api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":"MCI"}""");
        var fixture = game.Fixtures(competitionId: "PL-COMP", teamId: "MCI").First();
        api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{fixture.Id}}","seed":5}""");

        var reloaded = game.Persistence!.Load();

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.State.PlayerTeamId, Is.EqualTo("MCI"));
            Assert.That(reloaded.State.Fixtures.Single(f => f.Id == fixture.Id).IsPlayed, Is.True);
        });
    }

    [Test]
    public void AFullLeagueSeason_ProducesAConsistentTable()
    {
        var game = TestData.MakeLeagueWorld(teamCount: 10);
        var seed = 1;

        foreach (var fixture in game.State.Fixtures.OrderBy(f => f.DateUtc).ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { HighlightCount = 4 }, seed++);

        var table = game.GetLeagueTable("PL");

        Assert.Multiple(() =>
        {
            Assert.That(table.Select(r => r.Played), Is.All.EqualTo(18));
            Assert.That(table.Sum(r => r.Points), Is.EqualTo(table.Sum(r => r.Won) * 3 + table.Sum(r => r.Drawn)));
            Assert.That(table.Sum(r => r.Won), Is.EqualTo(table.Sum(r => r.Lost)));
            Assert.That(table.Sum(r => r.GoalsFor), Is.EqualTo(table.Sum(r => r.GoalsAgainst)));
            Assert.That(table.Sum(r => r.Drawn) % 2, Is.Zero);
            Assert.That(table.Select(r => r.Points), Is.Ordered.Descending);
            Assert.That(game.State.Fixtures.Select(f => f.IsPlayed), Is.All.True);
        });
    }

    [Test]
    public void AFullLeagueSeason_PlayerStatsMatchTheMatchesPlayed()
    {
        var game = TestData.MakeLeagueWorld(teamCount: 6);
        var seed = 1;

        foreach (var fixture in game.State.Fixtures.ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { HighlightCount = 4 }, seed++);

        var stats = game.State.PlayerStats.Values.ToList();

        Assert.Multiple(() =>
        {
            // 30 matches x 22 starters.
            Assert.That(stats.Sum(s => s.Appearances), Is.EqualTo(30 * 22));
            Assert.That(stats.Sum(s => s.Minutes), Is.EqualTo(30 * 22 * 90));
            Assert.That(stats.All(s => s.Starts == s.Appearances), Is.True);
            Assert.That(stats.Sum(s => s.Goals), Is.LessThanOrEqualTo(
                game.State.Fixtures.Sum(f => f.HomeGoals!.Value + f.AwayGoals!.Value)));
        });
    }

    [Test]
    public void ASeasonRollover_PromotesRelegatesAndRebuildsTheCalendar()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        var season = new SeasonEngine(game);
        season.GenerateDomesticSeason();

        var seed = 1;
        foreach (var fixture in game.State.Fixtures.Where(f => f.CompetitionId is "PL-COMP" or "CH-COMP").ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, seed++);

        var promoted = game.GetLeagueTable("CH").Take(3).Select(r => r.TeamId).ToList();
        var relegated = game.GetLeagueTable("PL").TakeLast(3).Select(r => r.TeamId).ToList();

        season.PromoteAndRelegate();
        game.State.Season++;
        season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues["PL"].TeamIds, Has.Count.EqualTo(20));
            Assert.That(game.State.Leagues["PL"].TeamIds, Is.SupersetOf(promoted));
            Assert.That(promoted.Select(id => game.State.Teams[id].LeagueId), Is.All.EqualTo("PL"));
            Assert.That(game.State.Leagues["CH"].TeamIds, Is.SupersetOf(relegated));
            Assert.That(relegated.Select(id => game.State.Teams[id].LeagueId), Is.All.EqualTo("CH"),
                "a relegated club stops in the division directly below");
            Assert.That(game.State.PlayerStats.Values.Select(s => s.Appearances), Is.All.Zero);
            Assert.That(game.State.PlayerStats.Values.Select(s => s.Season), Is.All.EqualTo(2027));
            Assert.That(game.State.Fixtures.Count(f => f.CompetitionId == "PL-COMP"), Is.EqualTo(380));
            Assert.That(game.State.Fixtures.Where(f => f.CompetitionId == "PL-COMP").Select(f => f.IsPlayed),
                Is.All.False);
        });
    }

    [Test]
    public void ASeasonRollover_KeepsLeagueMembershipAndTeamRecordsInStep()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        var season = new SeasonEngine(game);
        season.GenerateDomesticSeason();
        var seed = 1;
        foreach (var fixture in game.State.Fixtures.ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, seed++);

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues.Values.Sum(l => l.TeamIds.Count), Is.EqualTo(116),
                "no club may be lost or duplicated");
            Assert.That(game.State.Leagues.Values.SelectMany(l => l.TeamIds), Is.Unique);
            foreach (var league in game.State.Leagues.Values)
                foreach (var teamId in league.TeamIds)
                    Assert.That(game.State.Teams[teamId].LeagueId, Is.EqualTo(league.Id), teamId);
        });
    }

    [Test]
    public void ASeasonRollover_KeepsEveryDivisionTheSameSize()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        var season = new SeasonEngine(game);
        season.GenerateDomesticSeason();
        var seed = 1;
        foreach (var fixture in game.State.Fixtures.ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, seed++);

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues["PL"].TeamIds, Has.Count.EqualTo(20));
            Assert.That(game.State.Leagues["CH"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["L1"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["L2"].TeamIds, Has.Count.EqualTo(24));
            Assert.That(game.State.Leagues["NL"].TeamIds, Has.Count.EqualTo(24));
        });
    }

    [Test]
    public void ASeasonRollover_StaysStableOverSeveralSeasons()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        var season = new SeasonEngine(game);
        var seed = 1;

        for (var year = 0; year < 3; year++)
        {
            season.GenerateDomesticSeason();
            foreach (var fixture in game.State.Fixtures.ToList())
                game.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, seed++);
            season.PromoteAndRelegate();
            game.State.Season++;
        }

        Assert.Multiple(() =>
        {
            Assert.That(game.State.Leagues.Values.Select(l => l.TeamIds.Count),
                Is.EqualTo(new[] { 20, 24, 24, 24, 24 }));
            Assert.That(game.State.Leagues.Values.SelectMany(l => l.TeamIds), Is.Unique);
            foreach (var league in game.State.Leagues.Values)
                foreach (var teamId in league.TeamIds)
                    Assert.That(game.State.Teams[teamId].LeagueId, Is.EqualTo(league.Id), teamId);
        });
    }

    [Test]
    public void TransferWindow_MovesAPlayerAndTheWageBillFollows()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        var seller = game.State.Teams["LEI"];
        var buyer = game.State.Teams["ARS"];
        var target = seller.Players[10];
        var sellerWagesBefore = seller.Players.Sum(p => p.WeeklyWage);

        TransferEngine.Complete(seller, buyer, target, 5_000_000m, 120_000m, 4);
        var buyerBalance = buyer.Balance;
        new SeasonEngine(game).ProcessWeek();

        Assert.Multiple(() =>
        {
            Assert.That(buyer.Players, Does.Contain(target));
            Assert.That(seller.Players, Has.Count.EqualTo(21));
            Assert.That(buyer.Players, Has.Count.EqualTo(23));
            Assert.That(buyerBalance - buyer.Balance, Is.EqualTo(buyer.Players.Sum(p => p.WeeklyWage)));
            Assert.That(seller.Players.Sum(p => p.WeeklyWage), Is.LessThan(sellerWagesBefore));
        });
    }

    [Test]
    public void InjuryCycle_RemovesAndRestoresAPlayerAcrossWeeks()
    {
        var game = TestData.MakeLeagueWorld();
        var season = new SeasonEngine(game);
        game.SetPlayerInjury("T1-P01", 2);

        var fixture = game.State.Fixtures.First(f => f.HomeTeamId == "T1");
        game.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, 1);
        var appearancesWhileInjured = game.State.PlayerStats["T1-P01"].Appearances;

        season.ProcessWeek();
        season.ProcessWeek();
        var later = game.State.Fixtures.First(f => f.HomeTeamId == "T1" && !f.IsPlayed);
        game.SimulateFixture(later.Id, new MatchSimulationOptions { IncludeHighlights = false }, 2);

        Assert.Multiple(() =>
        {
            Assert.That(appearancesWhileInjured, Is.Zero);
            Assert.That(game.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01").Injured, Is.False);
            Assert.That(game.State.PlayerStats["T1-P01"].Appearances, Is.EqualTo(1));
            Assert.That(game.State.PlayerStats["T1-P01"].Injuries, Is.EqualTo(1));
        });
    }

    [Test]
    public void CupRun_EveryTieEventuallyProducesAWinner()
    {
        var game = UkDatabase.Create(_databasePath, loadExisting: false, autoSave: false);
        new SeasonEngine(game).GenerateFaCup();
        var options = new MatchSimulationOptions { IncludeHighlights = false };
        var seed = 1;

        // Play the first round, then any replays it produced.
        foreach (var tie in game.State.Fixtures.Where(f => f.CompetitionId == "FA").ToList())
            game.SimulateFixture(tie.Id, options, seed++);
        foreach (var replay in game.State.Fixtures.Where(f => f.CompetitionId == "FA" && !f.IsPlayed).ToList())
            game.SimulateFixture(replay.Id, options, seed++);

        var ties = game.State.Fixtures.Where(f => f.CompetitionId == "FA").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ties.Select(f => f.IsPlayed), Is.All.True);
            foreach (var group in ties.GroupBy(f => f.TieId))
            {
                var decider = group.OrderBy(f => f.DateUtc).Last();
                var settled = decider.HomeGoals != decider.AwayGoals ||
                              (decider.HomePenalties.HasValue && decider.HomePenalties != decider.AwayPenalties);
                Assert.That(settled, Is.True, $"tie {group.Key} has no winner");
            }
        });
    }

    [Test]
    public void ExportedState_CanRebuildTheEntireGameWithoutADatabase()
    {
        var game = StartNewGame();

        var restored = FootballGameEngine.ImportState(game.ExportState());

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.Teams, Has.Count.EqualTo(game.State.Teams.Count));
            Assert.That(restored.State.Fixtures, Has.Count.EqualTo(game.State.Fixtures.Count));
            Assert.That(restored.State.PlayerStats, Has.Count.EqualTo(game.State.PlayerStats.Count));
            Assert.That(
                restored.GetLeagueTable("PL").Select(r => r.TeamId),
                Is.EqualTo(game.GetLeagueTable("PL").Select(r => r.TeamId)));
            Assert.That(restored.Persistence, Is.Null);
        });
    }
}
