using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class GameApiTests
{
    private FootballGameEngine _game = null!;
    private GameApi _api = null!;

    [SetUp]
    public void SetUp()
    {
        _game = TestData.MakeLeagueWorld();
        _api = new GameApi(_game);
    }

    private static JsonElement Body(ApiResponse response) =>
        JsonDocument.Parse(response.Body).RootElement;

    private static string Error(ApiResponse response) =>
        Body(response).GetProperty("error").GetString()!;

    // ---------- Routing ----------

    [Test]
    public void Handle_AnUnknownPath_Returns404()
    {
        var response = _api.Handle("GET", "/api/nope");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
            Assert.That(Error(response), Is.EqualTo("API endpoint not found."));
        });
    }

    [Test]
    public void Handle_TheWrongVerbForAPath_Returns404()
    {
        Assert.That(_api.Handle("POST", GameApi.TeamsPath).StatusCode, Is.EqualTo(404));
    }

    [Test]
    public void Handle_NormalisesTheHttpVerb()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_api.Handle("get", GameApi.TeamsPath).StatusCode, Is.EqualTo(200));
            Assert.That(_api.Handle("  GeT  ", GameApi.TeamsPath).StatusCode, Is.EqualTo(200));
        });
    }

    [Test]
    public void Handle_IgnoresATrailingSlash()
    {
        Assert.That(_api.Handle("GET", GameApi.TeamsPath + "/").StatusCode, Is.EqualTo(200));
    }

    [Test]
    public void Handle_TheRootPath_Returns404()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_api.Handle("GET", "/").StatusCode, Is.EqualTo(404));
            Assert.That(_api.Handle("GET", "").StatusCode, Is.EqualTo(404));
        });
    }

    [Test]
    public void Handle_IsCaseSensitiveAboutPaths()
    {
        Assert.That(_api.Handle("GET", "/API/TEAMS").StatusCode, Is.EqualTo(404));
    }

    // ---------- GET /api/teams ----------

    [Test]
    public void GetTeams_ReturnsASummaryOfEveryClub()
    {
        var response = _api.Handle("GET", GameApi.TeamsPath);
        var teams = Body(response).GetProperty("teams");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(teams.GetArrayLength(), Is.EqualTo(4));
            Assert.That(teams[0].GetProperty("id").GetString(), Is.EqualTo("T1"));
            Assert.That(teams[0].GetProperty("shortName").GetString(), Is.EqualTo("T1"));
            Assert.That(teams[0].GetProperty("leagueId").GetString(), Is.EqualTo("PL"));
            Assert.That(teams[0].GetProperty("formation").GetString(), Is.EqualTo("F442"));
        });
    }

    [Test]
    public void GetTeams_OrdersByLeagueThenName()
    {
        _game.AddTeam(TestData.MakeTeam("Z9", "CH", name: "Aardvark FC"));

        var teams = Body(_api.Handle("GET", GameApi.TeamsPath)).GetProperty("teams");

        Assert.Multiple(() =>
        {
            Assert.That(teams[0].GetProperty("leagueId").GetString(), Is.EqualTo("CH"));
            Assert.That(teams[0].GetProperty("name").GetString(), Is.EqualTo("Aardvark FC"));
            Assert.That(teams[1].GetProperty("name").GetString(), Is.EqualTo("T1 FC"));
        });
    }

    [Test]
    public void GetTeams_WithNoClubs_ReturnsAnEmptyList()
    {
        var api = new GameApi(new FootballGameEngine());

        Assert.That(Body(api.Handle("GET", GameApi.TeamsPath)).GetProperty("teams").GetArrayLength(), Is.Zero);
    }

    // ---------- GET /api/game ----------

    [Test]
    public void GetGame_BeforeATeamIsChosen_ReturnsNulls()
    {
        var body = Body(_api.Handle("GET", GameApi.GamePath));

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("playerTeamId").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(body.GetProperty("playerTeam").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(body.GetProperty("season").GetInt32(), Is.EqualTo(2026));
        });
    }

    [Test]
    public void GetGame_AfterSelection_IncludesTheManagedClub()
    {
        _game.SelectPlayerTeam("T2");

        var body = Body(_api.Handle("GET", GameApi.GamePath));

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("playerTeamId").GetString(), Is.EqualTo("T2"));
            Assert.That(body.GetProperty("playerTeam").GetProperty("name").GetString(), Is.EqualTo("T2 FC"));
        });
    }

    // ---------- POST /api/game/select-team ----------

    [Test]
    public void SelectTeam_WithAValidId_Succeeds()
    {
        var response = _api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":"T3"}""");
        var body = Body(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("playerTeamId").GetString(), Is.EqualTo("T3"));
            Assert.That(body.GetProperty("playerTeam").GetProperty("id").GetString(), Is.EqualTo("T3"));
            Assert.That(_game.State.PlayerTeamId, Is.EqualTo("T3"));
        });
    }

    [Test]
    public void SelectTeam_WithAnUnknownId_Returns404()
    {
        var response = _api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":"GHOST"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
            Assert.That(Error(response), Does.Contain("GHOST"));
        });
    }

    [Test]
    public void SelectTeam_WithABlankId_Returns400()
    {
        var response = _api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":""}""");

        Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{\"teamId\":")]
    public void SelectTeam_WithAnUnusableBody_Returns400(string? body)
    {
        var response = _api.Handle("POST", GameApi.SelectTeamPath, body);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(Error(response), Is.Not.Empty);
        });
    }

    [Test]
    public void SelectTeam_WithJsonNull_Returns400()
    {
        var response = _api.Handle("POST", GameApi.SelectTeamPath, "null");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
            Assert.That(Error(response), Does.Contain("Request body is required."));
        });
    }

    // ---------- POST /api/game/formation ----------

    [Test]
    public void SetFormation_AppliesTheNewShape()
    {
        var response = _api.Handle("POST", GameApi.FormationPath, """{"teamId":"T1","formation":"F4231"}""");
        var body = Body(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("formation").GetString(), Is.EqualTo("F4231"));
            Assert.That(_game.GetFormation("T1"), Is.EqualTo(Formation.F4231));
        });
    }

    [Test]
    public void SetFormation_ForAnUnknownTeam_Returns404()
    {
        var response = _api.Handle("POST", GameApi.FormationPath, """{"teamId":"GHOST","formation":"F433"}""");

        Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
    }

    [Test]
    public void SetFormation_WithAnUnknownShape_Returns400()
    {
        var response = _api.Handle("POST", GameApi.FormationPath, """{"teamId":"T1","formation":"F999"}""");

        Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public void SetFormation_WithNoBody_Returns400()
    {
        Assert.That(_api.Handle("POST", GameApi.FormationPath).StatusCode, Is.EqualTo(400));
    }

    // ---------- POST /api/game/simulate ----------

    [Test]
    public void Simulate_PlaysTheFixtureAndReturnsIt()
    {
        var fixtureId = _game.State.Fixtures[0].Id;

        var response = _api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{fixtureId}}","seed":7}""");
        var body = Body(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(body.GetProperty("result").GetProperty("fixtureId").GetString(), Is.EqualTo(fixtureId));
            Assert.That(body.GetProperty("fixture").GetProperty("isPlayed").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("replayFixture").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(body.GetProperty("result").GetProperty("durationSeconds").GetInt32(), Is.EqualTo(8));
        });
    }

    [Test]
    public void Simulate_HonoursTheRequestedOptions()
    {
        var fixtureId = _game.State.Fixtures[0].Id;

        var body = Body(_api.Handle("POST", GameApi.SimulatePath,
            $$"""
            {"fixtureId":"{{fixtureId}}","durationSeconds":15,"includeHighlights":false,
             "highlightCount":3,"matchMinutes":45,"seed":9}
            """));

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("result").GetProperty("durationSeconds").GetInt32(), Is.EqualTo(15));
            Assert.That(body.GetProperty("result").GetProperty("highlights").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void Simulate_WithTheSameSeed_IsReproducible()
    {
        var fixtureId = _game.State.Fixtures[0].Id;
        var otherGame = TestData.MakeLeagueWorld();
        var otherFixtureId = otherGame.State.Fixtures[0].Id;

        var first = Body(_api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{fixtureId}}","seed":31}"""));
        var second = Body(new GameApi(otherGame).Handle(
            "POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{otherFixtureId}}","seed":31}"""));

        Assert.That(
            second.GetProperty("result").GetProperty("homeGoals").GetInt32(),
            Is.EqualTo(first.GetProperty("result").GetProperty("homeGoals").GetInt32()));
    }

    [Test]
    public void Simulate_ADrawnCupTie_ReturnsTheReplayFixture()
    {
        _game.AddCompetition(TestData.MakeCompetition(
            "FA", CompetitionType.FaCup, rules: new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 }));
        var tie = TestData.MakeFixture("FA", "T1", "T2");
        _game.State.Fixtures.Add(tie);
        var seed = TestData.FindSeed(
            tie, _game.State.Teams["T1"], _game.State.Teams["T2"],
            new MatchSimulationOptions(), r => r.HomeGoals == r.AwayGoals);

        var body = Body(_api.Handle("POST", GameApi.SimulatePath,
            $$"""{"fixtureId":"{{tie.Id}}","seed":{{seed}}}"""));

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("result").GetProperty("replayRequired").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("replayFixture").ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(body.GetProperty("replayFixture").GetProperty("homeTeamId").GetString(), Is.EqualTo("T2"));
        });
    }

    [Test]
    public void Simulate_AnUnknownFixture_Returns404()
    {
        var response = _api.Handle("POST", GameApi.SimulatePath, """{"fixtureId":"missing"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
            Assert.That(Error(response), Does.Contain("missing"));
        });
    }

    [Test]
    public void Simulate_AFixtureThatHasAlreadyBeenPlayed_Returns409()
    {
        var fixtureId = _game.State.Fixtures[0].Id;
        _api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{fixtureId}}","seed":1}""");

        var response = _api.Handle("POST", GameApi.SimulatePath, $$"""{"fixtureId":"{{fixtureId}}","seed":1}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.Conflict));
            Assert.That(Error(response), Does.Contain("already has a result"));
        });
    }

    [Test]
    public void Simulate_WithNoBody_Returns400()
    {
        Assert.That(_api.Handle("POST", GameApi.SimulatePath).StatusCode, Is.EqualTo(400));
    }

    // ---------- GET /api/game/fixtures ----------

    [Test]
    public void GetFixtures_ReturnsTheWholeCalendarOrderedByDate()
    {
        var fixtures = Body(_api.Handle("GET", GameApi.FixturesPath)).GetProperty("fixtures");

        var dates = Enumerable.Range(0, fixtures.GetArrayLength())
            .Select(i => fixtures[i].GetProperty("dateUtc").GetDateTime())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.GetArrayLength(), Is.EqualTo(12));
            Assert.That(dates, Is.Ordered);
            Assert.That(fixtures[0].GetProperty("competitionId").GetString(), Is.EqualTo("PL-COMP"));
            Assert.That(fixtures[0].GetProperty("isPlayed").GetBoolean(), Is.False);
            Assert.That(fixtures[0].GetProperty("homeGoals").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public void GetFixtures_IncludesTheResultOnceAMatchIsPlayed()
    {
        TestData.RecordResult(_game, _game.State.Fixtures[0], 2, 1);

        var fixtures = Body(_api.Handle("GET", GameApi.FixturesPath)).GetProperty("fixtures");
        var played = Enumerable.Range(0, fixtures.GetArrayLength())
            .Select(i => fixtures[i])
            .First(f => f.GetProperty("isPlayed").GetBoolean());

        Assert.Multiple(() =>
        {
            Assert.That(played.GetProperty("homeGoals").GetInt32(), Is.EqualTo(2));
            Assert.That(played.GetProperty("awayGoals").GetInt32(), Is.EqualTo(1));
        });
    }

    // ---------- GET /api/game/competitions ----------

    [Test]
    public void GetCompetitions_DescribesEachCompetitionAndItsRules()
    {
        var competitions = Body(_api.Handle("GET", GameApi.CompetitionsPath)).GetProperty("competitions");

        Assert.Multiple(() =>
        {
            Assert.That(competitions.GetArrayLength(), Is.EqualTo(1));
            Assert.That(competitions[0].GetProperty("id").GetString(), Is.EqualTo("PL-COMP"));
            Assert.That(competitions[0].GetProperty("type").GetString(), Is.EqualTo("League"));
            Assert.That(competitions[0].GetProperty("leagueId").GetString(), Is.EqualTo("PL"));
            Assert.That(competitions[0].GetProperty("teamIds").GetArrayLength(), Is.EqualTo(4));
            Assert.That(competitions[0].GetProperty("matchRules").GetProperty("replayAllowed").GetBoolean(), Is.False);
        });
    }

    // ---------- GET /api/game/standings ----------

    [Test]
    public void GetStandings_ReturnsATablePerLeague()
    {
        TestData.RecordResult(_game, _game.State.Fixtures.First(f => f.HomeTeamId == "T1"), 3, 0);

        var standings = Body(_api.Handle("GET", GameApi.StandingsPath)).GetProperty("standings");
        var table = standings.GetProperty("PL");

        Assert.Multiple(() =>
        {
            Assert.That(table.GetArrayLength(), Is.EqualTo(4));
            Assert.That(table[0].GetProperty("teamId").GetString(), Is.EqualTo("T1"));
            Assert.That(table[0].GetProperty("points").GetInt32(), Is.EqualTo(3));
            Assert.That(table[0].GetProperty("goalDifference").GetInt32(), Is.EqualTo(3));
        });
    }

    [Test]
    public void GetStandings_WithNoLeagues_ReturnsAnEmptyMap()
    {
        var api = new GameApi(new FootballGameEngine());

        Assert.That(
            Body(api.Handle("GET", GameApi.StandingsPath)).GetProperty("standings").EnumerateObject().Count(),
            Is.Zero);
    }

    // ---------- GET /api/game/player-stats ----------

    [Test]
    public void GetPlayerStats_JoinsStatsToPlayerDetail()
    {
        TestData.RecordResult(_game, _game.State.Fixtures[0], 1, 0);

        var stats = Body(_api.Handle("GET", GameApi.PlayerStatsPath)).GetProperty("playerStats");

        Assert.Multiple(() =>
        {
            Assert.That(stats.GetArrayLength(), Is.EqualTo(44));
            Assert.That(stats[0].GetProperty("name").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(stats[0].GetProperty("position").GetString(), Is.Not.Empty);
            Assert.That(stats[0].GetProperty("injured").GetBoolean(), Is.False);
        });
    }

    [Test]
    public void GetPlayerStats_OrdersByGoalsThenAppearances()
    {
        TestData.RecordResult(_game, _game.State.Fixtures[0], 1, 0);
        _game.State.PlayerStats["T3-P05"].Goals = 9;

        var stats = Body(_api.Handle("GET", GameApi.PlayerStatsPath)).GetProperty("playerStats");
        var goals = Enumerable.Range(0, stats.GetArrayLength())
            .Select(i => stats[i].GetProperty("goals").GetInt32())
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(stats[0].GetProperty("playerId").GetString(), Is.EqualTo("T3-P05"));
            Assert.That(goals, Is.Ordered.Descending);
        });
    }

    [Test]
    public void GetPlayerStats_DropsStatsWithNoMatchingPlayer()
    {
        _game.State.PlayerStats["GHOST"] = new PlayerSeasonStats { PlayerId = "GHOST", Goals = 99 };

        var stats = Body(_api.Handle("GET", GameApi.PlayerStatsPath)).GetProperty("playerStats");

        Assert.That(stats.GetArrayLength(), Is.EqualTo(44));
    }

    [Test]
    public void GetPlayerStats_ReportsInjuryAndSuspensionState()
    {
        _game.SetPlayerInjury("T1-P01", 5);
        _game.State.Teams["T1"].Players[1].SuspensionMatches = 2;

        var stats = Body(_api.Handle("GET", GameApi.PlayerStatsPath)).GetProperty("playerStats");
        var rows = Enumerable.Range(0, stats.GetArrayLength()).Select(i => stats[i]).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single(r => r.GetProperty("playerId").GetString() == "T1-P01")
                .GetProperty("injuryWeeks").GetInt32(), Is.EqualTo(5));
            Assert.That(rows.Single(r => r.GetProperty("playerId").GetString() == "T1-P02")
                .GetProperty("suspensionMatches").GetInt32(), Is.EqualTo(2));
        });
    }

    // ---------- Save endpoints ----------

    [Test]
    public void Save_WithoutPersistence_Returns409()
    {
        var response = _api.Handle("POST", GameApi.SavePath, """{"slot":"career"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.Conflict));
            Assert.That(Error(response), Does.Contain("SQLite"));
        });
    }

    [Test]
    public void SaveSlots_WithoutPersistence_ReturnsAnEmptyList()
    {
        var response = _api.Handle("GET", GameApi.SaveSlotsPath);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(Body(response).GetProperty("slots").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void PathConstants_AreTheDocumentedRoutes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GameApi.TeamsPath, Is.EqualTo("/api/teams"));
            Assert.That(GameApi.GamePath, Is.EqualTo("/api/game"));
            Assert.That(GameApi.SelectTeamPath, Is.EqualTo("/api/game/select-team"));
            Assert.That(GameApi.FormationPath, Is.EqualTo("/api/game/formation"));
            Assert.That(GameApi.SimulatePath, Is.EqualTo("/api/game/simulate"));
            Assert.That(GameApi.FixturesPath, Is.EqualTo("/api/game/fixtures"));
            Assert.That(GameApi.CompetitionsPath, Is.EqualTo("/api/game/competitions"));
            Assert.That(GameApi.StandingsPath, Is.EqualTo("/api/game/standings"));
            Assert.That(GameApi.PlayerStatsPath, Is.EqualTo("/api/game/player-stats"));
            Assert.That(GameApi.SavePath, Is.EqualTo("/api/game/save"));
            Assert.That(GameApi.SaveSlotsPath, Is.EqualTo("/api/game/save-slots"));
        });
    }
}

[TestFixture]
public class GameApiPersistenceTests
{
    private string _databasePath = null!;
    private GamePersistence _persistence = null!;
    private GameApi _api = null!;

    [SetUp]
    public void SetUp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fme-tests");
        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, $"{Guid.NewGuid():N}.db");
        _persistence = new GamePersistence(_databasePath);
        _api = new GameApi(TestData.MakeLeagueWorld(persistence: _persistence));
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { /* harmless temp file */ }
    }

    private static JsonElement Body(ApiResponse response) => JsonDocument.Parse(response.Body).RootElement;

    [Test]
    public void Save_WithASlot_WritesThatSlot()
    {
        var response = _api.Handle("POST", GameApi.SavePath, """{"slot":"career"}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(Body(response).GetProperty("slot").GetString(), Is.EqualTo("career"));
            Assert.That(_persistence.Exists("career"), Is.True);
        });
    }

    [Test]
    public void Save_WithNoBody_UsesTheDefaultSlot()
    {
        var response = _api.Handle("POST", GameApi.SavePath);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(Body(response).GetProperty("slot").GetString(), Is.EqualTo("default"));
            Assert.That(_persistence.Exists(), Is.True);
        });
    }

    [Test]
    public void Save_WithAnExplicitNullSlot_FallsBackToTheDefault()
    {
        var response = _api.Handle("POST", GameApi.SavePath, """{"slot":null}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(_persistence.Exists(), Is.True);
        });
    }

    [Test]
    public void Save_WithABlankSlot_Returns400()
    {
        var response = _api.Handle("POST", GameApi.SavePath, """{"slot":"  "}""");

        Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public void SaveSlots_ListsWhatHasBeenSaved()
    {
        _api.Handle("POST", GameApi.SavePath, """{"slot":"alpha"}""");
        _api.Handle("POST", GameApi.SavePath, """{"slot":"beta"}""");

        var slots = Body(_api.Handle("GET", GameApi.SaveSlotsPath)).GetProperty("slots");

        Assert.That(
            Enumerable.Range(0, slots.GetArrayLength()).Select(i => slots[i].GetString()),
            Is.EquivalentTo(new[] { "alpha", "beta" }));
    }

    [Test]
    public void Save_ThenLoad_RestoresTheGameThroughTheApi()
    {
        _api.Handle("POST", GameApi.SelectTeamPath, """{"teamId":"T4"}""");
        _api.Handle("POST", GameApi.SavePath, """{"slot":"career"}""");

        var restored = new GameApi(_persistence.Load("career"));
        var body = Body(restored.Handle("GET", GameApi.GamePath));

        Assert.That(body.GetProperty("playerTeamId").GetString(), Is.EqualTo("T4"));
    }
}
