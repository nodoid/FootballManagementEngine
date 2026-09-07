using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class GamePersistenceTests
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
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_databasePath + suffix); }
            catch (IOException) { /* the OS still holds the handle; the temp file is harmless */ }
        }
    }

    private GamePersistence NewPersistence() => new(_databasePath);

    private SqliteConnection OpenRead()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        return connection;
    }

    private long ScalarCount(string sql)
    {
        using var connection = OpenRead();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    // ---------- Construction and schema ----------

    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithABlankPath_Throws(string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GamePersistence(path));

        Assert.That(exception!.ParamName, Is.EqualTo("databasePath"));
    }

    [Test]
    public void Constructor_CreatesTheDatabaseFileAndSchema()
    {
        NewPersistence();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_databasePath), Is.True);
            Assert.That(ScalarCount(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
                "('GameSaves','LeagueStandings','PlayerStats','CompetitionState')"),
                Is.EqualTo(4));
        });
    }

    [Test]
    public void Constructor_CreatesTheReportingIndexes()
    {
        NewPersistence();

        Assert.That(ScalarCount(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name IN " +
            "('IX_LeagueStandings_SaveLeague','IX_PlayerStats_SaveTeam')"),
            Is.EqualTo(2));
    }

    [Test]
    public void Constructor_OnAnExistingDatabase_IsIdempotent()
    {
        var first = NewPersistence();
        first.Save(TestData.MakeLeagueWorld());

        var second = new GamePersistence(_databasePath);

        Assert.That(second.Exists(), Is.True, "re-opening must not wipe existing saves");
    }

    [Test]
    public void DefaultDatabasePath_IsTheProjectDatabase()
    {
        Assert.That(GamePersistence.DefaultDatabasePath, Is.EqualTo("football-management.db"));
    }

    // ---------- Exists / GetSlots ----------

    [Test]
    public void Exists_OnAFreshDatabase_IsFalse()
    {
        Assert.That(NewPersistence().Exists(), Is.False);
    }

    [Test]
    public void Exists_AfterSaving_IsTrueForThatSlotOnly()
    {
        var persistence = NewPersistence();
        persistence.Save(TestData.MakeLeagueWorld(), "career");

        Assert.Multiple(() =>
        {
            Assert.That(persistence.Exists("career"), Is.True);
            Assert.That(persistence.Exists("other"), Is.False);
        });
    }

    [Test]
    public void GetSlots_OnAFreshDatabase_IsEmpty()
    {
        Assert.That(NewPersistence().GetSlots(), Is.Empty);
    }

    [Test]
    public void GetSlots_ListsEverySavedSlot()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();
        persistence.Save(game, "alpha");
        persistence.Save(game, "beta");

        Assert.That(persistence.GetSlots(), Is.EquivalentTo(new[] { "alpha", "beta" }));
    }

    // ---------- Save ----------

    [TestCase("")]
    [TestCase("  ")]
    public void Save_WithABlankSlot_Throws(string slot)
    {
        var persistence = NewPersistence();

        var exception = Assert.Throws<ArgumentException>(() => persistence.Save(TestData.MakeLeagueWorld(), slot));

        Assert.That(exception!.ParamName, Is.EqualTo("slot"));
    }

    [Test]
    public void Save_StoresTheHeaderFields()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();
        game.SelectPlayerTeam("T2");
        game.State.Season = 2033;

        persistence.Save(game, "career");

        using var connection = OpenRead();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Season, PlayerTeamId, Slot, StateJson FROM GameSaves";
        using var reader = command.ExecuteReader();

        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetInt32(0), Is.EqualTo(2033));
            Assert.That(reader.GetString(1), Is.EqualTo("T2"));
            Assert.That(reader.GetString(2), Is.EqualTo("career"));
            Assert.That(reader.GetString(3), Does.Contain("\"season\": 2033"));
        });
    }

    [Test]
    public void Save_WithNoManagerSelected_StoresANullTeam()
    {
        NewPersistence().Save(TestData.MakeLeagueWorld());

        Assert.That(ScalarCount("SELECT COUNT(*) FROM GameSaves WHERE PlayerTeamId IS NULL"), Is.EqualTo(1));
    }

    [Test]
    public void Save_WritesOneStandingsRowPerTeam()
    {
        var game = TestData.MakeLeagueWorld();
        TestData.RecordResult(game, game.State.Fixtures[0], 2, 1);

        NewPersistence().Save(game);

        using var connection = OpenRead();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TeamId, TeamName, Played, Won, Points FROM LeagueStandings ORDER BY Points DESC";
        using var reader = command.ExecuteReader();
        reader.Read();

        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(1), Does.EndWith("FC"));
            Assert.That(reader.GetInt32(2), Is.EqualTo(1));
            Assert.That(reader.GetInt32(3), Is.EqualTo(1));
            Assert.That(reader.GetInt32(4), Is.EqualTo(3));
        });
        Assert.That(ScalarCount("SELECT COUNT(*) FROM LeagueStandings"), Is.EqualTo(4));
    }

    [Test]
    public void Save_WritesOneRowPerPlayerStatLine()
    {
        var game = TestData.MakeLeagueWorld();
        game.SetPlayerInjury("T1-P01", 2);

        NewPersistence().Save(game);

        Assert.Multiple(() =>
        {
            Assert.That(ScalarCount("SELECT COUNT(*) FROM PlayerStats"), Is.EqualTo(44));
            Assert.That(ScalarCount("SELECT Injuries FROM PlayerStats WHERE PlayerId = 'T1-P01'"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Save_WritesEveryCompetition()
    {
        var game = TestData.MakeLeagueWorld();
        game.AddCompetition(TestData.MakeCompetition(
            "FA", CompetitionType.FaCup, teamIds: ["T1", "T2"],
            rules: new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 2 }));

        NewPersistence().Save(game);

        using var connection = OpenRead();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Type, LeagueId, MatchRulesJson, TeamIdsJson FROM CompetitionState WHERE CompetitionId='FA'";
        using var reader = command.ExecuteReader();
        reader.Read();

        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("FaCup"));
            Assert.That(reader.IsDBNull(1), Is.True);
            Assert.That(reader.GetString(2), Does.Contain("\"maxReplays\": 2"));
            Assert.That(reader.GetString(3), Does.Contain("T1"));
        });
        Assert.That(ScalarCount("SELECT COUNT(*) FROM CompetitionState"), Is.EqualTo(2));
    }

    [Test]
    public void Save_TwiceToTheSameSlot_KeepsOneSaveRow()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();

        persistence.Save(game);
        game.State.Season = 2040;
        persistence.Save(game);

        Assert.Multiple(() =>
        {
            Assert.That(ScalarCount("SELECT COUNT(*) FROM GameSaves"), Is.EqualTo(1));
            Assert.That(ScalarCount("SELECT Season FROM GameSaves"), Is.EqualTo(2040));
            Assert.That(persistence.GetSlots(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Save_ToDistinctSlots_KeepsBoth()
    {
        var persistence = NewPersistence();
        var first = TestData.MakeLeagueWorld();
        first.State.Season = 2026;
        var second = TestData.MakeLeagueWorld();
        second.State.Season = 2027;

        persistence.Save(first, "a");
        persistence.Save(second, "b");

        Assert.Multiple(() =>
        {
            Assert.That(persistence.Load("a").State.Season, Is.EqualTo(2026));
            Assert.That(persistence.Load("b").State.Season, Is.EqualTo(2027));
        });
    }

    [Test]
    public void Save_OverwritingASlot_CascadeDeletesTheOldDetailRows()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();

        persistence.Save(game);
        persistence.Save(game);

        Assert.Multiple(() =>
        {
            Assert.That(ScalarCount("SELECT COUNT(*) FROM LeagueStandings"), Is.EqualTo(4));
            Assert.That(ScalarCount("SELECT COUNT(*) FROM PlayerStats"), Is.EqualTo(44));
            Assert.That(ScalarCount("SELECT COUNT(*) FROM CompetitionState"), Is.EqualTo(1));
            Assert.That(ScalarCount(
                "SELECT COUNT(*) FROM LeagueStandings WHERE SaveId NOT IN (SELECT Id FROM GameSaves)"),
                Is.Zero, "detail rows must not outlive their save");
        });
    }

    [Test]
    public void Save_IsAtomicAcrossAllTables()
    {
        var game = TestData.MakeLeagueWorld();

        NewPersistence().Save(game);

        using var connection = OpenRead();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(DISTINCT SaveId) FROM LeagueStandings),
                   (SELECT COUNT(DISTINCT SaveId) FROM PlayerStats),
                   (SELECT COUNT(DISTINCT SaveId) FROM CompetitionState),
                   (SELECT Id FROM GameSaves)
            """;
        using var reader = command.ExecuteReader();
        reader.Read();

        Assert.Multiple(() =>
        {
            Assert.That(reader.GetInt32(0), Is.EqualTo(1));
            Assert.That(reader.GetInt32(1), Is.EqualTo(1));
            Assert.That(reader.GetInt32(2), Is.EqualTo(1));
            Assert.That(reader.GetString(3), Has.Length.EqualTo(32));
        });
    }

    // ---------- Load ----------

    [Test]
    public void Load_FromAnEmptyDatabase_Throws()
    {
        var exception = Assert.Throws<FileNotFoundException>(() => NewPersistence().Load());

        Assert.That(exception!.Message, Does.Contain("default"));
    }

    [Test]
    public void Load_FromAnUnknownSlot_Throws()
    {
        var persistence = NewPersistence();
        persistence.Save(TestData.MakeLeagueWorld(), "career");

        Assert.Throws<FileNotFoundException>(() => persistence.Load("other"));
    }

    [Test]
    public void Load_RestoresTheSavedGame()
    {
        var persistence = NewPersistence();
        var original = TestData.MakeLeagueWorld();
        original.SelectPlayerTeam("T3");
        original.SetFormation("T3", Formation.F451);
        TestData.RecordResult(original, original.State.Fixtures[0], 3, 1);
        persistence.Save(original, "career");

        var restored = persistence.Load("career");

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.PlayerTeamId, Is.EqualTo("T3"));
            Assert.That(restored.State.Teams, Has.Count.EqualTo(4));
            Assert.That(restored.State.Teams["T3"].Formation, Is.EqualTo(Formation.F451));
            Assert.That(restored.State.Fixtures, Has.Count.EqualTo(12));
            Assert.That(restored.State.Fixtures.Count(f => f.IsPlayed), Is.EqualTo(1));
            Assert.That(restored.GetLeagueTable("PL")[0].Points, Is.EqualTo(3));
        });
    }

    [Test]
    public void Load_AttachesPersistenceSoTheGameCanSaveAgain()
    {
        var persistence = NewPersistence();
        persistence.Save(TestData.MakeLeagueWorld(), "career");

        var restored = persistence.Load("career");
        restored.State.Season = 2050;
        restored.Save("career");

        Assert.Multiple(() =>
        {
            Assert.That(restored.Persistence, Is.SameAs(persistence));
            Assert.That(persistence.Load("career").State.Season, Is.EqualTo(2050));
        });
    }

    [Test]
    public void Load_EnablesAutoSaveByDefault()
    {
        var persistence = NewPersistence();
        persistence.Save(TestData.MakeLeagueWorld(), "career");

        Assert.That(persistence.Load("career").AutoSave, Is.True);
    }

    [Test]
    public void Load_CanDisableAutoSave()
    {
        var persistence = NewPersistence();
        persistence.Save(TestData.MakeLeagueWorld(), "career");

        Assert.That(persistence.Load("career", autoSave: false).AutoSave, Is.False);
    }

    [Test]
    public void Load_AfterAnOverwrite_ReturnsTheLatestState()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();

        persistence.Save(game, "career");
        game.SelectPlayerTeam("T4");
        persistence.Save(game, "career");

        Assert.That(persistence.Load("career").State.PlayerTeamId, Is.EqualTo("T4"));
    }

    // ---------- Auto-save wiring ----------

    [Test]
    public void AutoSave_PersistsWhenTheManagerPicksAClub()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence, autoSave: true);

        game.SelectPlayerTeam("T1");

        Assert.That(persistence.Load().State.PlayerTeamId, Is.EqualTo("T1"));
    }

    [Test]
    public void AutoSave_PersistsWhenAResultIsApplied()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence, autoSave: true);

        TestData.RecordResult(game, game.State.Fixtures[0], 1, 0);

        Assert.That(persistence.Load().State.Fixtures.Count(f => f.IsPlayed), Is.EqualTo(1));
    }

    [Test]
    public void AutoSave_PersistsFormationAndInjuryChanges()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence, autoSave: true);

        game.SetFormation("T1", Formation.F343);
        game.SetPlayerInjury("T1-P02", 6);

        var restored = persistence.Load();

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.Teams["T1"].Formation, Is.EqualTo(Formation.F343));
            Assert.That(restored.State.Teams["T1"].Players.Single(p => p.Id == "T1-P02").InjuryWeeks, Is.EqualTo(6));
        });
    }

    [Test]
    public void AutoSave_Disabled_LeavesTheDatabaseUntouched()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence, autoSave: false);

        game.SelectPlayerTeam("T1");

        Assert.That(persistence.Exists(), Is.False);
    }

    [Test]
    public void SaveIfConfigured_WithPersistence_WritesAndReturnsTrue()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence);

        Assert.Multiple(() =>
        {
            Assert.That(game.SaveIfConfigured("manual"), Is.True);
            Assert.That(persistence.Exists("manual"), Is.True);
        });
    }

    [Test]
    public void ProcessWeek_AutoSavesThroughTheSeasonEngine()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld(persistence: persistence);
        var start = game.State.CurrentDateUtc;

        new SeasonEngine(game).ProcessWeek();

        Assert.That(persistence.Load().State.CurrentDateUtc, Is.EqualTo(start.AddDays(7)));
    }

    [Test]
    public void SaveThenLoad_SurvivesAFullSeasonOfResults()
    {
        var persistence = NewPersistence();
        var game = TestData.MakeLeagueWorld();
        var seed = 1;
        foreach (var fixture in game.State.Fixtures.ToList())
            game.SimulateFixture(fixture.Id, new MatchSimulationOptions { HighlightCount = 3, InjuryChance = 0 }, seed++);
        persistence.Save(game, "season");

        var restored = persistence.Load("season");

        Assert.Multiple(() =>
        {
            Assert.That(restored.State.Fixtures.Count(f => f.IsPlayed), Is.EqualTo(12));
            Assert.That(restored.GetLeagueTable("PL").Sum(r => r.Played), Is.EqualTo(24));
            Assert.That(
                restored.GetLeagueTable("PL").Select(r => r.Points),
                Is.EqualTo(game.GetLeagueTable("PL").Select(r => r.Points)));
            Assert.That(restored.State.PlayerStats.Values.Sum(s => s.Appearances), Is.EqualTo(12 * 22));
        });
    }
}
