using System.Text.Json;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class GameEngineStateTests
{
    [Test]
    public void Constructor_WithoutState_StartsANewGame()
    {
        var engine = new FootballGameEngine();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State, Is.Not.Null);
            Assert.That(engine.State.Season, Is.EqualTo(2026));
            Assert.That(engine.Persistence, Is.Null);
            Assert.That(engine.AutoSave, Is.False);
        });
    }

    [Test]
    public void Constructor_AdoptsTheSuppliedState()
    {
        var state = new GameState { Season = 2031 };

        var engine = new FootballGameEngine(state);

        Assert.That(engine.State, Is.SameAs(state));
    }

    [Test]
    public void Constructor_SeedsSeasonStatsForPlayersAlreadyInTheState()
    {
        var state = new GameState { Season = 2030 };
        state.Teams["A"] = TestData.MakeTeam("A", squadSize: 3);

        var engine = new FootballGameEngine(state);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.PlayerStats, Has.Count.EqualTo(3));
            Assert.That(engine.State.PlayerStats["A-P01"].TeamId, Is.EqualTo("A"));
            Assert.That(engine.State.PlayerStats["A-P01"].Season, Is.EqualTo(2030));
            Assert.That(engine.State.PlayerStats["A-P01"].Appearances, Is.Zero);
        });
    }

    [Test]
    public void Constructor_KeepsExistingStatsRatherThanResettingThem()
    {
        var state = new GameState();
        state.Teams["A"] = TestData.MakeTeam("A", squadSize: 2);
        state.PlayerStats["A-P01"] = new PlayerSeasonStats { PlayerId = "A-P01", TeamId = "A", Goals = 14 };

        var engine = new FootballGameEngine(state);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.PlayerStats["A-P01"].Goals, Is.EqualTo(14));
            Assert.That(engine.State.PlayerStats, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void JsonOptions_UseCamelCaseAndStringEnums()
    {
        var engine = new FootballGameEngine();

        Assert.Multiple(() =>
        {
            Assert.That(engine.JsonOptions.PropertyNamingPolicy, Is.EqualTo(JsonNamingPolicy.CamelCase));
            Assert.That(engine.JsonOptions.WriteIndented, Is.True);
        });
    }

    [Test]
    public void AddTeam_RegistersTheTeamAndItsStats()
    {
        var engine = new FootballGameEngine();

        engine.AddTeam(TestData.MakeTeam("A", squadSize: 4));

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams, Does.ContainKey("A"));
            Assert.That(engine.State.PlayerStats, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void AddTeam_WithAnExistingId_ReplacesTheTeam()
    {
        var engine = new FootballGameEngine();
        engine.AddTeam(TestData.MakeTeam("A", name: "Old FC"));

        engine.AddTeam(TestData.MakeTeam("A", name: "New FC"));

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams, Has.Count.EqualTo(1));
            Assert.That(engine.State.Teams["A"].Name, Is.EqualTo("New FC"));
        });
    }

    [Test]
    public void AddLeague_AndAddCompetition_RegisterByIdentifier()
    {
        var engine = new FootballGameEngine();

        engine.AddLeague(TestData.MakeLeague("PL"));
        engine.AddCompetition(TestData.MakeCompetition("PL-COMP", CompetitionType.League, "PL"));

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues, Does.ContainKey("PL"));
            Assert.That(engine.State.Competitions["PL-COMP"].LeagueId, Is.EqualTo("PL"));
        });
    }

    [Test]
    public void SelectPlayerTeam_StoresAndReturnsTheClub()
    {
        var engine = TestData.MakeLeagueWorld();

        var team = engine.SelectPlayerTeam("T2");

        Assert.Multiple(() =>
        {
            Assert.That(team.Id, Is.EqualTo("T2"));
            Assert.That(engine.State.PlayerTeamId, Is.EqualTo("T2"));
            Assert.That(engine.GetPlayerTeam(), Is.SameAs(team));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void SelectPlayerTeam_WithABlankId_Throws(string teamId)
    {
        var engine = TestData.MakeLeagueWorld();

        Assert.Throws<ArgumentException>(() => engine.SelectPlayerTeam(teamId));
    }

    [Test]
    public void SelectPlayerTeam_WithAnUnknownId_Throws()
    {
        var engine = TestData.MakeLeagueWorld();

        var exception = Assert.Throws<KeyNotFoundException>(() => engine.SelectPlayerTeam("NOPE"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("NOPE"));
            Assert.That(engine.State.PlayerTeamId, Is.Null);
        });
    }

    [Test]
    public void GetPlayerTeam_BeforeSelection_ReturnsNull()
    {
        Assert.That(TestData.MakeLeagueWorld().GetPlayerTeam(), Is.Null);
    }

    [Test]
    public void GetPlayerTeam_WhenTheStoredTeamNoLongerExists_ReturnsNull()
    {
        var engine = TestData.MakeLeagueWorld();
        engine.SelectPlayerTeam("T1");
        engine.State.Teams.Remove("T1");

        Assert.That(engine.GetPlayerTeam(), Is.Null);
    }

    [Test]
    public void SetFormation_ChangesTheTeamShape()
    {
        var engine = TestData.MakeLeagueWorld();

        engine.SetFormation("T1", Formation.F4231);

        Assert.Multiple(() =>
        {
            Assert.That(engine.GetFormation("T1"), Is.EqualTo(Formation.F4231));
            Assert.That(engine.State.Teams["T1"].Formation, Is.EqualTo(Formation.F4231));
        });
    }

    [Test]
    public void SetFormation_ForAnUnknownTeam_Throws()
    {
        var engine = TestData.MakeLeagueWorld();

        Assert.Throws<KeyNotFoundException>(() => engine.SetFormation("NOPE", Formation.F433));
    }

    [Test]
    public void GetFormation_ForAnUnknownTeam_Throws()
    {
        var engine = TestData.MakeLeagueWorld();

        Assert.Throws<KeyNotFoundException>(() => engine.GetFormation("NOPE"));
    }

    [Test]
    public void GetFormation_DefaultsToFourFourTwo()
    {
        Assert.That(TestData.MakeLeagueWorld().GetFormation("T1"), Is.EqualTo(Formation.F442));
    }

    [Test]
    public void SetPlayerInjury_MarksThePlayerOutAndCountsTheInjury()
    {
        var engine = TestData.MakeLeagueWorld();

        engine.SetPlayerInjury("T1-P01", 3);

        var player = engine.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01");

        Assert.Multiple(() =>
        {
            Assert.That(player.Injured, Is.True);
            Assert.That(player.InjuryWeeks, Is.EqualTo(3));
            Assert.That(engine.State.PlayerStats["T1-P01"].Injuries, Is.EqualTo(1));
        });
    }

    [Test]
    public void SetPlayerInjury_WithZeroWeeks_ClearsTheInjuryWithoutCountingIt()
    {
        var engine = TestData.MakeLeagueWorld();
        engine.SetPlayerInjury("T1-P01", 2);

        engine.SetPlayerInjury("T1-P01", 0);

        var player = engine.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01");

        Assert.Multiple(() =>
        {
            Assert.That(player.Injured, Is.False);
            Assert.That(player.InjuryWeeks, Is.Zero);
            Assert.That(engine.State.PlayerStats["T1-P01"].Injuries, Is.EqualTo(1));
        });
    }

    [Test]
    public void SetPlayerInjury_CountsEachSeparateInjury()
    {
        var engine = TestData.MakeLeagueWorld();

        engine.SetPlayerInjury("T1-P01", 2);
        engine.SetPlayerInjury("T1-P01", 5);

        Assert.That(engine.State.PlayerStats["T1-P01"].Injuries, Is.EqualTo(2));
    }

    [Test]
    public void SetPlayerInjury_WithNegativeWeeks_Throws()
    {
        var engine = TestData.MakeLeagueWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetPlayerInjury("T1-P01", -1));
    }

    [Test]
    public void SetPlayerInjury_ForAnUnknownPlayer_Throws()
    {
        var engine = TestData.MakeLeagueWorld();

        var exception = Assert.Throws<KeyNotFoundException>(() => engine.SetPlayerInjury("GHOST", 1));

        Assert.That(exception!.Message, Does.Contain("GHOST"));
    }

    [Test]
    public void ResetSeasonPlayerStats_ClearsAggregatesButKeepsEveryPlayer()
    {
        var engine = TestData.MakeLeagueWorld();
        engine.State.PlayerStats["T1-P01"].Goals = 20;
        engine.State.PlayerStats["T1-P01"].Appearances = 30;

        engine.ResetSeasonPlayerStats();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.PlayerStats["T1-P01"].Goals, Is.Zero);
            Assert.That(engine.State.PlayerStats["T1-P01"].Appearances, Is.Zero);
            Assert.That(engine.State.PlayerStats, Has.Count.EqualTo(4 * 11));
        });
    }

    [Test]
    public void ResetSeasonPlayerStats_StampsTheCurrentSeason()
    {
        var engine = TestData.MakeLeagueWorld();
        engine.State.Season = 2029;

        engine.ResetSeasonPlayerStats();

        Assert.That(engine.State.PlayerStats.Values.Select(s => s.Season), Is.All.EqualTo(2029));
    }

    [Test]
    public void ResetSeasonPlayerStats_DropsStatsForPlayersWhoHaveLeft()
    {
        var engine = TestData.MakeLeagueWorld();
        engine.State.PlayerStats["RETIRED"] = new PlayerSeasonStats { PlayerId = "RETIRED" };

        engine.ResetSeasonPlayerStats();

        Assert.That(engine.State.PlayerStats, Does.Not.ContainKey("RETIRED"));
    }

    [Test]
    public void Save_WithoutPersistence_Throws()
    {
        var engine = new FootballGameEngine();

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Save());

        Assert.That(exception!.Message, Does.Contain("SQLite"));
    }

    [Test]
    public void SaveIfConfigured_WithoutPersistence_ReturnsFalse()
    {
        Assert.That(new FootballGameEngine().SaveIfConfigured(), Is.False);
    }
}

[TestFixture]
public class GameEngineFixtureQueryTests
{
    private FootballGameEngine _engine = null!;

    [SetUp]
    public void SetUp() => _engine = TestData.MakeLeagueWorld();

    [Test]
    public void Fixtures_WithNoFilters_ReturnsEverythingOrderedByDate()
    {
        var fixtures = _engine.Fixtures().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(12));
            Assert.That(fixtures.Select(f => f.DateUtc), Is.Ordered);
        });
    }

    [Test]
    public void Fixtures_FiltersByCompetition()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));
        _engine.State.Fixtures.Add(TestData.MakeFixture("FA", "T1", "T2"));

        Assert.Multiple(() =>
        {
            Assert.That(_engine.Fixtures(competitionId: "FA").Count(), Is.EqualTo(1));
            Assert.That(_engine.Fixtures(competitionId: "PL-COMP").Count(), Is.EqualTo(12));
            Assert.That(_engine.Fixtures(competitionId: "NOPE"), Is.Empty);
        });
    }

    [Test]
    public void Fixtures_FiltersByTeamAcrossHomeAndAway()
    {
        var forTeam = _engine.Fixtures(teamId: "T1").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(forTeam, Has.Count.EqualTo(6));
            Assert.That(forTeam.All(f => f.HomeTeamId == "T1" || f.AwayTeamId == "T1"), Is.True);
            Assert.That(forTeam.Count(f => f.HomeTeamId == "T1"), Is.EqualTo(3));
        });
    }

    [Test]
    public void Fixtures_FiltersByDateWindowInclusively()
    {
        var opening = _engine.Fixtures().First().DateUtc;

        var window = _engine.Fixtures(fromUtc: opening, toUtc: opening).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(window, Has.Count.EqualTo(2));
            Assert.That(window.Select(f => f.DateUtc), Is.All.EqualTo(opening));
        });
    }

    [Test]
    public void Fixtures_FiltersFromADateOnwards()
    {
        var opening = _engine.Fixtures().First().DateUtc;

        var later = _engine.Fixtures(fromUtc: opening.AddDays(1)).ToList();

        Assert.That(later, Has.Count.EqualTo(10));
    }

    [Test]
    public void Fixtures_CombinesFilters()
    {
        var opening = _engine.Fixtures().First().DateUtc;

        var result = _engine.Fixtures("PL-COMP", "T1", opening, opening.AddDays(8)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.All(f => f.HomeTeamId == "T1" || f.AwayTeamId == "T1"), Is.True);
        });
    }

    [Test]
    public void Fixtures_WithNoMatches_ReturnsEmpty()
    {
        Assert.That(_engine.Fixtures(fromUtc: new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)), Is.Empty);
    }

    [Test]
    public void GetLeagueTable_ReflectsRecordedResults()
    {
        var fixture = _engine.State.Fixtures.First(f => f.HomeTeamId == "T1");
        TestData.RecordResult(_engine, fixture, 3, 0);

        var table = _engine.GetLeagueTable("PL");

        Assert.Multiple(() =>
        {
            Assert.That(table, Has.Count.EqualTo(4));
            Assert.That(table[0].TeamId, Is.EqualTo("T1"));
            Assert.That(table[0].Points, Is.EqualTo(3));
            Assert.That(table[0].GoalsFor, Is.EqualTo(3));
        });
    }

    [Test]
    public void GetLeagueTable_IgnoresCupResults()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));
        var cupTie = TestData.MakeFixture("FA", "T1", "T2");
        _engine.State.Fixtures.Add(cupTie);
        TestData.RecordResult(_engine, cupTie, 5, 0);

        var table = _engine.GetLeagueTable("PL");

        Assert.That(table.Select(r => r.Played), Is.All.Zero);
    }

    [Test]
    public void GetLeagueTable_ForAnUnknownLeague_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _engine.GetLeagueTable("NOPE"));
    }

    [Test]
    public void GetLeagueTable_WithNoLeagueCompetition_ReturnsAZeroedTable()
    {
        _engine.State.Competitions.Remove("PL-COMP");

        var table = _engine.GetLeagueTable("PL");

        Assert.Multiple(() =>
        {
            Assert.That(table, Has.Count.EqualTo(4));
            Assert.That(table.Select(r => r.Played), Is.All.Zero);
        });
    }
}

[TestFixture]
public class GameEngineApplyResultTests
{
    private FootballGameEngine _engine = null!;
    private Fixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = TestData.MakeLeagueWorld();
        _fixture = _engine.State.Fixtures.First();
    }

    [Test]
    public void ApplyResult_RecordsTheScoreOnTheFixture()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 2, AwayGoals = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(_fixture.IsPlayed, Is.True);
            Assert.That(_fixture.HomeGoals, Is.EqualTo(2));
            Assert.That(_fixture.AwayGoals, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyResult_RecordsExtraTimeAndPenalties()
    {
        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 1,
            ExtraTime = true, HomePenalties = 5, AwayPenalties = 4
        });

        Assert.Multiple(() =>
        {
            Assert.That(_fixture.ExtraTimePlayed, Is.True);
            Assert.That(_fixture.HomePenalties, Is.EqualTo(5));
            Assert.That(_fixture.AwayPenalties, Is.EqualTo(4));
        });
    }

    [Test]
    public void ApplyResult_WithADate_RescheduleTheFixture()
    {
        var kickOff = new DateTime(2027, 3, 3, 19, 45, 0, DateTimeKind.Utc);

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0, DateUtc = kickOff });

        Assert.That(_fixture.DateUtc, Is.EqualTo(kickOff));
    }

    [Test]
    public void ApplyResult_WithoutADate_KeepsTheScheduledKickOff()
    {
        var original = _fixture.DateUtc;

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0 });

        Assert.That(_fixture.DateUtc, Is.EqualTo(original));
    }

    [Test]
    public void ApplyResult_ForAnUnknownFixture_Throws()
    {
        var exception = Assert.Throws<KeyNotFoundException>(
            () => _engine.ApplyResult(new MatchResult { FixtureId = "missing" }));

        Assert.That(exception!.Message, Does.Contain("missing"));
    }

    [Test]
    public void ApplyResult_Twice_Throws()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 0 });

        var exception = Assert.Throws<InvalidOperationException>(
            () => _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 2, AwayGoals = 0 }));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("already has a result"));
            Assert.That(_fixture.HomeGoals, Is.EqualTo(1));
        });
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void ApplyResult_WithNegativeGoals_Throws(int homeGoals, int awayGoals)
    {
        Assert.Throws<ArgumentException>(() => _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = homeGoals, AwayGoals = awayGoals
        }));

        Assert.That(_fixture.IsPlayed, Is.False);
    }

    [Test]
    public void ApplyResult_CreditsElevenStartersWithAnAppearance()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 0 });

        var homeStats = _engine.State.Teams[_fixture.HomeTeamId].Players
            .Select(p => _engine.State.PlayerStats[p.Id]).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(homeStats.Count(s => s.Appearances == 1), Is.EqualTo(11));
            Assert.That(homeStats.Select(s => s.Starts), Is.All.EqualTo(1));
            Assert.That(homeStats.Select(s => s.Minutes), Is.All.EqualTo(90));
        });
    }

    [Test]
    public void ApplyResult_CapsTheStartingElevenAtEleven()
    {
        var engine = new FootballGameEngine();
        engine.AddTeam(TestData.MakeTeam("BIG", squadSize: 25));
        engine.AddTeam(TestData.MakeTeam("AWY"));
        engine.AddCompetition(TestData.MakeCompetition("C"));
        var fixture = TestData.MakeFixture("C", "BIG", "AWY");
        engine.State.Fixtures.Add(fixture);

        engine.ApplyResult(new MatchResult { FixtureId = fixture.Id, HomeGoals = 0, AwayGoals = 1 });

        var appearances = engine.State.Teams["BIG"].Players
            .Count(p => engine.State.PlayerStats[p.Id].Appearances == 1);

        Assert.That(appearances, Is.EqualTo(11));
    }

    [Test]
    public void ApplyResult_SkipsInjuredAndSuspendedPlayers()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        home.Players[0].Injured = true;
        home.Players[1].SuspensionMatches = 1;

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0 });

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats[home.Players[0].Id].Appearances, Is.Zero);
            Assert.That(_engine.State.PlayerStats[home.Players[1].Id].Appearances, Is.Zero);
            Assert.That(_engine.State.PlayerStats[home.Players[2].Id].Appearances, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyResult_WithNoAvailablePlayers_RecordsNoAppearances()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        foreach (var player in home.Players) player.Injured = true;

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 3 });

        Assert.That(home.Players.Select(p => _engine.State.PlayerStats[p.Id].Appearances), Is.All.Zero);
    }

    [Test]
    public void ApplyResult_CreatesStatsForAPlayerAddedAfterTheSeasonStarted()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var signing = TestData.MakePlayer("NEW-P01");
        home.Players.Insert(0, signing);

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0 });

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats, Does.ContainKey("NEW-P01"));
            Assert.That(_engine.State.PlayerStats["NEW-P01"].Appearances, Is.EqualTo(1));
            Assert.That(_engine.State.PlayerStats["NEW-P01"].TeamId, Is.EqualTo(home.Id));
        });
    }

    [Test]
    public void ApplyResult_CreditsEachGoalToAnAttackingPlayer()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 2, AwayGoals = 0,
            Highlights =
            [
                new MatchHighlight { Minute = 10, TeamId = home.Id, Type = MatchEventType.Goal },
                new MatchHighlight { Minute = 70, TeamId = home.Id, Type = MatchEventType.Goal }
            ]
        });

        var scorers = home.Players
            .Where(p => _engine.State.PlayerStats[p.Id].Goals > 0)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.Goals), Is.EqualTo(2));
            Assert.That(scorers.Select(p => p.Position), Is.All.Not.EqualTo(Position.GK));
            Assert.That(scorers.Select(p => p.Position), Is.All.AnyOf(Position.MID, Position.FWD, Position.DEF));
        });
    }

    [Test]
    public void ApplyResult_NeverCreditsAGoalToTheGoalkeeper()
    {
        // Every minute of a match, so any minute-derived scorer would be caught.
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var keeper = home.Players.First(p => p.Position == Position.GK);
        var highlights = Enumerable.Range(1, 90)
            .Select(minute => new MatchHighlight { Minute = minute, TeamId = home.Id, Type = MatchEventType.Goal })
            .ToList();

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 90, AwayGoals = 0, Highlights = highlights
        });

        Assert.That(_engine.State.PlayerStats[keeper.Id].Goals, Is.Zero);
    }

    [Test]
    public void ApplyResult_FavoursForwardsOverDefenders()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var highlights = Enumerable.Range(1, 90)
            .Select(minute => new MatchHighlight { Minute = minute, TeamId = home.Id, Type = MatchEventType.Goal })
            .ToList();

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 90, AwayGoals = 0, Highlights = highlights
        });

        var byPosition = home.Players
            .GroupBy(p => p.Position)
            .ToDictionary(g => g.Key, g => g.Sum(p => _engine.State.PlayerStats[p.Id].Goals));

        Assert.Multiple(() =>
        {
            Assert.That(byPosition.GetValueOrDefault(Position.FWD), Is.GreaterThan(byPosition.GetValueOrDefault(Position.DEF)));
            Assert.That(byPosition.GetValueOrDefault(Position.MID), Is.GreaterThan(byPosition.GetValueOrDefault(Position.DEF)));
            Assert.That(byPosition.GetValueOrDefault(Position.GK), Is.Zero);
        });
    }

    [Test]
    public void ApplyResult_AttributesTheSameGoalToTheSamePlayerEveryTime()
    {
        var highlights = new List<MatchHighlight>
        {
            new() { Minute = 23, TeamId = "T1", Type = MatchEventType.Goal },
            new() { Minute = 64, TeamId = "T1", Type = MatchEventType.Goal }
        };

        static Dictionary<string, int> Play(List<MatchHighlight> goals)
        {
            var engine = TestData.MakeLeagueWorld();
            var fixture = engine.State.Fixtures.First(f => f.HomeTeamId == "T1");
            engine.ApplyResult(new MatchResult
            {
                FixtureId = fixture.Id, HomeGoals = 2, AwayGoals = 0, Highlights = goals
            });
            return engine.State.Teams["T1"].Players
                .ToDictionary(p => p.Id, p => engine.State.PlayerStats[p.Id].Goals);
        }

        Assert.That(Play(highlights), Is.EqualTo(Play(highlights)));
    }

    [Test]
    public void ApplyResult_WithOnlyAGoalkeeperAvailable_StillCreditsTheGoal()
    {
        var engine = new FootballGameEngine();
        var solo = TestData.MakeTeam("SOLO", squadSize: 1);
        engine.AddTeam(solo);
        engine.AddTeam(TestData.MakeTeam("AWY"));
        engine.AddCompetition(TestData.MakeCompetition("C"));
        var fixture = TestData.MakeFixture("C", "SOLO", "AWY");
        engine.State.Fixtures.Add(fixture);

        engine.ApplyResult(new MatchResult
        {
            FixtureId = fixture.Id, HomeGoals = 1, AwayGoals = 0,
            Highlights = [new MatchHighlight { Minute = 30, TeamId = "SOLO", Type = MatchEventType.Goal }]
        });

        Assert.Multiple(() =>
        {
            Assert.That(solo.Players[0].Position, Is.EqualTo(Position.GK));
            Assert.That(engine.State.PlayerStats[solo.Players[0].Id].Goals, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyResult_CreditsNoMoreGoalsThanWereScored()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 0,
            Highlights =
            [
                new MatchHighlight { Minute = 10, TeamId = home.Id, Type = MatchEventType.Goal },
                new MatchHighlight { Minute = 20, TeamId = home.Id, Type = MatchEventType.Goal },
                new MatchHighlight { Minute = 30, TeamId = home.Id, Type = MatchEventType.Goal }
            ]
        });

        Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.Goals), Is.EqualTo(1));
    }

    [Test]
    public void ApplyResult_WithoutGoalHighlights_AttributesNoScorers()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 3, AwayGoals = 0 });

        Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.Goals), Is.Zero);
    }

    [Test]
    public void ApplyResult_IgnoresGoalHighlightsBelongingToTheOtherTeam()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var away = _engine.State.Teams[_fixture.AwayTeamId];

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 0,
            Highlights = [new MatchHighlight { Minute = 10, TeamId = away.Id, Type = MatchEventType.Goal }]
        });

        Assert.Multiple(() =>
        {
            Assert.That(home.Players.Sum(p => _engine.State.PlayerStats[p.Id].Goals), Is.Zero);
            Assert.That(away.Players.Sum(p => _engine.State.PlayerStats[p.Id].Goals), Is.Zero,
                "the away team did not score, so no away goal is credited");
        });
    }

    [Test]
    public void ApplyResult_RecordsYellowCardsAgainstAStarter()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0,
            Highlights =
            [
                new MatchHighlight { Minute = 25, TeamId = home.Id, Type = MatchEventType.YellowCard },
                new MatchHighlight { Minute = 55, TeamId = home.Id, Type = MatchEventType.YellowCard }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(home.Players.Sum(p => _engine.State.PlayerStats[p.Id].YellowCards), Is.EqualTo(2));
            Assert.That(_engine.State.PlayerStats[home.Players[25 % 11].Id].YellowCards, Is.EqualTo(1));
            Assert.That(_engine.State.PlayerStats[home.Players[55 % 11].Id].YellowCards, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyResult_IgnoresNonScoringNonCardHighlights()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];

        _engine.ApplyResult(new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0,
            Highlights =
            [
                new MatchHighlight { Minute = 5, TeamId = home.Id, Type = MatchEventType.Save },
                new MatchHighlight { Minute = 6, TeamId = home.Id, Type = MatchEventType.Chance }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.Goals), Is.Zero);
            Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.YellowCards), Is.Zero);
        });
    }

    [Test]
    public void ApplyResult_CreditsACleanSheetToTheSideThatConcededNothing()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 2, AwayGoals = 0 });

        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var away = _engine.State.Teams[_fixture.AwayTeamId];

        Assert.Multiple(() =>
        {
            Assert.That(home.Players.Sum(p => _engine.State.PlayerStats[p.Id].CleanSheets), Is.EqualTo(11),
                "the winners conceded nothing");
            Assert.That(away.Players.Sum(p => _engine.State.PlayerStats[p.Id].CleanSheets), Is.Zero,
                "failing to score is not a clean sheet");
        });
    }

    [Test]
    public void ApplyResult_AGoallessDraw_CreditsBothSides()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 0, AwayGoals = 0 });

        var home = _engine.State.Teams[_fixture.HomeTeamId];
        var away = _engine.State.Teams[_fixture.AwayTeamId];

        Assert.Multiple(() =>
        {
            Assert.That(home.Players.Sum(p => _engine.State.PlayerStats[p.Id].CleanSheets), Is.EqualTo(11));
            Assert.That(away.Players.Sum(p => _engine.State.PlayerStats[p.Id].CleanSheets), Is.EqualTo(11));
        });
    }

    [Test]
    public void ApplyResult_AScoringDraw_CreditsNeitherSide()
    {
        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 1 });

        Assert.That(_engine.State.PlayerStats.Values.Sum(s => s.CleanSheets), Is.Zero);
    }

    [Test]
    public void ApplyResult_OnlyStartersEarnACleanSheet()
    {
        var home = _engine.State.Teams[_fixture.HomeTeamId];
        home.Players[0].Injured = true;

        _engine.ApplyResult(new MatchResult { FixtureId = _fixture.Id, HomeGoals = 1, AwayGoals = 0 });

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats[home.Players[0].Id].CleanSheets, Is.Zero);
            Assert.That(home.Players.Sum(p => _engine.State.PlayerStats[p.Id].CleanSheets), Is.EqualTo(10));
        });
    }

    [Test]
    public void ApplyResult_WithAnUnknownTeamOnTheFixture_SkipsStatsWithoutThrowing()
    {
        var orphan = TestData.MakeFixture("PL-COMP", "GHOST", "T1");
        _engine.State.Fixtures.Add(orphan);

        Assert.DoesNotThrow(() => _engine.ApplyResult(new MatchResult
        {
            FixtureId = orphan.Id, HomeGoals = 1, AwayGoals = 1
        }));

        Assert.That(orphan.IsPlayed, Is.True);
    }

    [Test]
    public void ApplyResultsJson_AppliesEveryResultInTheBatch()
    {
        var first = _engine.State.Fixtures[0];
        var second = _engine.State.Fixtures[1];

        _engine.ApplyResultsJson($$"""
        [
          {"fixtureId":"{{first.Id}}","homeGoals":3,"awayGoals":1},
          {"fixtureId":"{{second.Id}}","homeGoals":0,"awayGoals":2}
        ]
        """);

        Assert.Multiple(() =>
        {
            Assert.That(first.HomeGoals, Is.EqualTo(3));
            Assert.That(second.AwayGoals, Is.EqualTo(2));
        });
    }

    [Test]
    public void ApplyResultsJson_WithAnEmptyArray_DoesNothing()
    {
        _engine.ApplyResultsJson("[]");

        Assert.That(_engine.State.Fixtures.Select(f => f.IsPlayed), Is.All.False);
    }

    [Test]
    public void ApplyResultsJson_WithJsonNull_Throws()
    {
        Assert.Throws<ArgumentException>(() => _engine.ApplyResultsJson("null"));
    }

    [Test]
    public void ApplyResultsJson_WithMalformedJson_Throws()
    {
        Assert.Throws<JsonException>(() => _engine.ApplyResultsJson("{not json"));
    }

    [Test]
    public void ApplyResultsJson_WithAnUnknownFixture_Throws()
    {
        Assert.Throws<KeyNotFoundException>(
            () => _engine.ApplyResultsJson("""[{"fixtureId":"missing","homeGoals":1,"awayGoals":0}]"""));
    }

    [Test]
    public void ApplyResultsJson_RoundTripsAResultSerialisedByTheEngine()
    {
        var result = new MatchResult
        {
            FixtureId = _fixture.Id, HomeGoals = 2, AwayGoals = 2, ExtraTime = true,
            HomePenalties = 4, AwayPenalties = 3,
            Highlights = [new MatchHighlight { Minute = 8, TeamId = _fixture.HomeTeamId, Type = MatchEventType.Goal }]
        };

        _engine.ApplyResultsJson(JsonSerializer.Serialize(new[] { result }, _engine.JsonOptions));

        Assert.Multiple(() =>
        {
            Assert.That(_fixture.HomeGoals, Is.EqualTo(2));
            Assert.That(_fixture.ExtraTimePlayed, Is.True);
            Assert.That(_fixture.HomePenalties, Is.EqualTo(4));
        });
    }
}

[TestFixture]
public class GameEngineImportExportTests
{
    [Test]
    public void ExportState_ProducesCamelCaseJson()
    {
        var engine = TestData.MakeLeagueWorld();

        var json = engine.ExportState();

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"season\""));
            Assert.That(json, Does.Contain("\"teams\""));
            Assert.That(json, Does.Not.Contain("\"Season\""));
        });
    }

    [Test]
    public void ImportState_RestoresAnExportedGame()
    {
        var original = TestData.MakeLeagueWorld();
        original.SelectPlayerTeam("T3");
        original.SetFormation("T3", Formation.F352);
        TestData.RecordResult(original, original.State.Fixtures[0], 4, 2);

        var restored = FootballGameEngine.ImportState(original.ExportState());

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.PlayerTeamId, Is.EqualTo("T3"));
            Assert.That(restored.State.Season, Is.EqualTo(original.State.Season));
            Assert.That(restored.State.CurrentDateUtc, Is.EqualTo(original.State.CurrentDateUtc));
            Assert.That(restored.State.Teams, Has.Count.EqualTo(4));
            Assert.That(restored.State.Teams["T3"].Formation, Is.EqualTo(Formation.F352));
            Assert.That(restored.State.Fixtures, Has.Count.EqualTo(12));
            Assert.That(restored.State.Leagues["PL"].TeamIds, Has.Count.EqualTo(4));
            Assert.That(restored.State.Competitions["PL-COMP"].Type, Is.EqualTo(CompetitionType.League));
        });
    }

    [Test]
    public void ImportState_PreservesFixtureIdentityAndResults()
    {
        var original = TestData.MakeLeagueWorld();
        var played = original.State.Fixtures[0];
        TestData.RecordResult(original, played, 1, 1);

        var restored = FootballGameEngine.ImportState(original.ExportState());
        var restoredFixture = restored.State.Fixtures.Single(f => f.Id == played.Id);

        Assert.Multiple(() =>
        {
            Assert.That(restoredFixture.IsPlayed, Is.True);
            Assert.That(restoredFixture.HomeGoals, Is.EqualTo(1));
            Assert.That(restoredFixture.AwayGoals, Is.EqualTo(1));
            Assert.That(restoredFixture.DateUtc, Is.EqualTo(played.DateUtc));
        });
    }

    [Test]
    public void ImportState_PreservesPlayerStats()
    {
        var original = TestData.MakeLeagueWorld();
        original.SetPlayerInjury("T1-P02", 4);
        TestData.RecordResult(original, original.State.Fixtures[0], 0, 0);

        var restored = FootballGameEngine.ImportState(original.ExportState());

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.PlayerStats, Has.Count.EqualTo(original.State.PlayerStats.Count));
            Assert.That(restored.State.PlayerStats["T1-P02"].Injuries, Is.EqualTo(1));
            Assert.That(restored.State.Teams["T1"].Players.Single(p => p.Id == "T1-P02").InjuryWeeks, Is.EqualTo(4));
        });
    }

    [Test]
    public void ImportState_IsCaseInsensitiveAboutPropertyNames()
    {
        var engine = FootballGameEngine.ImportState("""{"SEASON":2040,"PlayerTeamId":"ARS"}""");

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Season, Is.EqualTo(2040));
            Assert.That(engine.State.PlayerTeamId, Is.EqualTo("ARS"));
        });
    }

    [Test]
    public void ImportState_WithAnEmptyObject_ProducesADefaultGame()
    {
        var engine = FootballGameEngine.ImportState("{}");

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Season, Is.EqualTo(2026));
            Assert.That(engine.State.Teams, Is.Empty);
        });
    }

    [Test]
    public void ImportState_WithJsonNull_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => FootballGameEngine.ImportState("null"));

        Assert.That(exception!.Message, Does.Contain("Invalid save game"));
    }

    [Test]
    public void ImportState_WithMalformedJson_Throws()
    {
        Assert.Throws<JsonException>(() => FootballGameEngine.ImportState("{"));
    }

    [Test]
    public void ImportState_WithoutPersistence_LeavesSavingDisabled()
    {
        var engine = FootballGameEngine.ImportState("{}");

        Assert.Multiple(() =>
        {
            Assert.That(engine.Persistence, Is.Null);
            Assert.That(engine.AutoSave, Is.False);
            Assert.That(engine.SaveIfConfigured(), Is.False);
        });
    }

    [Test]
    public void ImportState_BackfillsStatsForPlayersMissingFromTheSave()
    {
        var engine = FootballGameEngine.ImportState("""
        {"teams":{"A":{"id":"A","name":"A FC","players":[{"id":"A-P01","name":"Keeper"}]}}}
        """);

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.PlayerStats, Does.ContainKey("A-P01"));
            Assert.That(engine.State.PlayerStats["A-P01"].TeamId, Is.EqualTo("A"));
        });
    }

    [Test]
    public void ExportThenImport_IsStableAcrossTwoRoundTrips()
    {
        var original = TestData.MakeLeagueWorld();
        original.SelectPlayerTeam("T1");

        var once = FootballGameEngine.ImportState(original.ExportState());
        var twice = FootballGameEngine.ImportState(once.ExportState());

        Assert.That(twice.ExportState(), Is.EqualTo(once.ExportState()));
    }
}
