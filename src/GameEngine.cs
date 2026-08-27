using System.Text.Json;
using System.Text.Json.Serialization;

namespace FootballManagementEngine;

public sealed class FootballGameEngine
{
    public GameState State { get; }
    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FootballGameEngine(GameState? state = null) =>
        State = state ?? new GameState();

    public void AddTeam(Team team) => State.Teams[team.Id] = team;

    /// <summary>
    /// Selects the club managed by the human player.
    /// </summary>
    public Team SelectPlayerTeam(string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
            throw new ArgumentException("Team ID is required.", nameof(teamId));

        if (!State.Teams.TryGetValue(teamId, out var team))
            throw new KeyNotFoundException($"Team '{teamId}' not found.");

        State.PlayerTeamId = team.Id;
        return team;
    }

    public Team? GetPlayerTeam() =>
        State.PlayerTeamId != null && State.Teams.TryGetValue(State.PlayerTeamId, out var team)
            ? team
            : null;
    public void AddLeague(League league) => State.Leagues[league.Id] = league;
    public void AddCompetition(Competition competition) =>
        State.Competitions[competition.Id] = competition;

    public void ApplyResult(MatchResult result)
    {
        if (result.HomeGoals < 0 || result.AwayGoals < 0)
            throw new ArgumentException("Goals cannot be negative.");

        var fixture = State.Fixtures.SingleOrDefault(f => f.Id == result.FixtureId)
            ?? throw new KeyNotFoundException($"Fixture '{result.FixtureId}' not found.");

        if (fixture.IsPlayed)
            throw new InvalidOperationException("Fixture already has a result.");

        fixture.IsPlayed = true;
        fixture.HomeGoals = result.HomeGoals;
        fixture.AwayGoals = result.AwayGoals;
        fixture.ExtraTimePlayed = result.ExtraTime;
        fixture.HomePenalties = result.HomePenalties;
        fixture.AwayPenalties = result.AwayPenalties;

        if (result.DateUtc.HasValue)
            fixture.DateUtc = result.DateUtc.Value;
    }

    public void ApplyResultsJson(string json)
    {
        var results = JsonSerializer.Deserialize<List<MatchResult>>(json, JsonOptions)
            ?? throw new ArgumentException("Invalid results JSON.");

        foreach (var result in results)
            ApplyResult(result);
    }

    public List<StandingRow> GetLeagueTable(string leagueId)
    {
        var league = State.Leagues[leagueId];
        var competitionIds = State.Competitions.Values
            .Where(c => c.Type == CompetitionType.League && c.LeagueId == leagueId)
            .Select(c => c.Id)
            .ToHashSet();

        return LeagueTable.Build(
            league, State.Teams,
            State.Fixtures.Where(f => competitionIds.Contains(f.CompetitionId)));
    }

    public IEnumerable<Fixture> Fixtures(
        string? competitionId = null,
        string? teamId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        return State.Fixtures
            .Where(f => competitionId == null || f.CompetitionId == competitionId)
            .Where(f => teamId == null || f.HomeTeamId == teamId || f.AwayTeamId == teamId)
            .Where(f => fromUtc == null || f.DateUtc >= fromUtc)
            .Where(f => toUtc == null || f.DateUtc <= toUtc)
            .OrderBy(f => f.DateUtc);
    }

    public string ExportState() => JsonSerializer.Serialize(State, JsonOptions);

    public static FootballGameEngine ImportState(string json)
    {
        var state = JsonSerializer.Deserialize<GameState>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            }) ?? throw new ArgumentException("Invalid save game.");

        return new FootballGameEngine(state);
    }
}
