using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace FootballManagementEngine;

/// <summary>SQLite persistence for complete game saves and queryable season data.</summary>
public sealed class GamePersistence
{
    public const string DefaultDatabasePath = "football-management.db";
    private readonly string _connectionString;

    public GamePersistence(string databasePath = DefaultDatabasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        EnsureSchema();
    }

    public void Save(FootballGameEngine game, string slot = "default")
    {
        if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("Save slot is required.", nameof(slot));
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        var saveId = Guid.NewGuid().ToString("N");
        var stateJson = game.ExportState();
        var now = DateTime.UtcNow.ToString("O");

        Execute(connection, transaction, "DELETE FROM GameSaves WHERE Slot = $slot", ("$slot", slot));
        Execute(connection, transaction,
            "INSERT INTO GameSaves(Id, Slot, Season, CurrentDateUtc, PlayerTeamId, SavedAtUtc, StateJson) VALUES($id,$slot,$season,$date,$team,$saved,$json)",
            ("$id", saveId), ("$slot", slot), ("$season", game.State.Season),
            ("$date", game.State.CurrentDateUtc.ToString("O")), ("$team", (object?)game.State.PlayerTeamId ?? DBNull.Value),
            ("$saved", now), ("$json", stateJson));

        foreach (var league in game.State.Leagues.Values)
        {
            foreach (var row in game.GetLeagueTable(league.Id))
            {
                Execute(connection, transaction,
                    "INSERT INTO LeagueStandings(SaveId, LeagueId, TeamId, TeamName, Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst, Points) VALUES($save,$league,$team,$name,$played,$won,$drawn,$lost,$gf,$ga,$points)",
                    ("$save", saveId), ("$league", league.Id), ("$team", row.TeamId), ("$name", row.TeamName),
                    ("$played", row.Played), ("$won", row.Won), ("$drawn", row.Drawn), ("$lost", row.Lost),
                    ("$gf", row.GoalsFor), ("$ga", row.GoalsAgainst), ("$points", row.Points));
            }
        }

        foreach (var stats in game.State.PlayerStats.Values)
        {
            Execute(connection, transaction,
                "INSERT INTO PlayerStats(SaveId, PlayerId, TeamId, Season, Appearances, Starts, Minutes, Goals, Assists, YellowCards, RedCards, CleanSheets, Injuries) VALUES($save,$player,$team,$season,$apps,$starts,$mins,$goals,$assists,$yellow,$red,$clean,$injuries)",
                ("$save", saveId), ("$player", stats.PlayerId), ("$team", stats.TeamId), ("$season", stats.Season),
                ("$apps", stats.Appearances), ("$starts", stats.Starts), ("$mins", stats.Minutes), ("$goals", stats.Goals),
                ("$assists", stats.Assists), ("$yellow", stats.YellowCards), ("$red", stats.RedCards),
                ("$clean", stats.CleanSheets), ("$injuries", stats.Injuries));
        }

        foreach (var competition in game.State.Competitions.Values)
        {
            Execute(connection, transaction,
                "INSERT INTO CompetitionState(SaveId, CompetitionId, Name, Type, LeagueId, MatchRulesJson, TeamIdsJson) VALUES($save,$id,$name,$type,$league,$rules,$teams)",
                ("$save", saveId), ("$id", competition.Id), ("$name", competition.Name), ("$type", competition.Type.ToString()),
                ("$league", (object?)competition.LeagueId ?? DBNull.Value),
                ("$rules", JsonSerializer.Serialize(competition.MatchRules, game.JsonOptions)),
                ("$teams", JsonSerializer.Serialize(competition.TeamIds, game.JsonOptions)));
        }

        transaction.Commit();
    }

    public bool Exists(string slot = "default")
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM GameSaves WHERE Slot = $slot LIMIT 1";
        command.Parameters.AddWithValue("$slot", slot);
        return command.ExecuteScalar() != null;
    }

    public FootballGameEngine Load(string slot = "default", bool autoSave = true)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StateJson FROM GameSaves WHERE Slot = $slot ORDER BY SavedAtUtc DESC LIMIT 1";
        command.Parameters.AddWithValue("$slot", slot);
        var json = command.ExecuteScalar() as string
            ?? throw new FileNotFoundException($"No saved game exists for slot '{slot}'.");
        return FootballGameEngine.ImportState(json, this, autoSave);
    }

    public IReadOnlyList<string> GetSlots()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Slot FROM GameSaves ORDER BY SavedAtUtc DESC";
        using var reader = command.ExecuteReader();
        var slots = new List<string>();
        while (reader.Read()) slots.Add(reader.GetString(0));
        return slots;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE IF NOT EXISTS GameSaves (
            Id TEXT PRIMARY KEY, Slot TEXT NOT NULL UNIQUE, Season INTEGER NOT NULL,
            CurrentDateUtc TEXT NOT NULL, PlayerTeamId TEXT NULL, SavedAtUtc TEXT NOT NULL, StateJson TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS LeagueStandings (
            SaveId TEXT NOT NULL, LeagueId TEXT NOT NULL, TeamId TEXT NOT NULL, TeamName TEXT NOT NULL,
            Played INTEGER NOT NULL, Won INTEGER NOT NULL, Drawn INTEGER NOT NULL, Lost INTEGER NOT NULL,
            GoalsFor INTEGER NOT NULL, GoalsAgainst INTEGER NOT NULL, Points INTEGER NOT NULL,
            PRIMARY KEY(SaveId, LeagueId, TeamId), FOREIGN KEY(SaveId) REFERENCES GameSaves(Id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS PlayerStats (
            SaveId TEXT NOT NULL, PlayerId TEXT NOT NULL, TeamId TEXT NOT NULL, Season INTEGER NOT NULL,
            Appearances INTEGER NOT NULL, Starts INTEGER NOT NULL, Minutes INTEGER NOT NULL, Goals INTEGER NOT NULL,
            Assists INTEGER NOT NULL, YellowCards INTEGER NOT NULL, RedCards INTEGER NOT NULL, CleanSheets INTEGER NOT NULL,
            Injuries INTEGER NOT NULL, PRIMARY KEY(SaveId, PlayerId), FOREIGN KEY(SaveId) REFERENCES GameSaves(Id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS CompetitionState (
            SaveId TEXT NOT NULL, CompetitionId TEXT NOT NULL, Name TEXT NOT NULL, Type TEXT NOT NULL,
            LeagueId TEXT NULL, MatchRulesJson TEXT NOT NULL, TeamIdsJson TEXT NOT NULL,
            PRIMARY KEY(SaveId, CompetitionId), FOREIGN KEY(SaveId) REFERENCES GameSaves(Id) ON DELETE CASCADE);
        CREATE INDEX IF NOT EXISTS IX_LeagueStandings_SaveLeague ON LeagueStandings(SaveId, LeagueId, Points DESC);
        CREATE INDEX IF NOT EXISTS IX_PlayerStats_SaveTeam ON PlayerStats(SaveId, TeamId);
        """;
        command.ExecuteNonQuery();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
