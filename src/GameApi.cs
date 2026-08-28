using System.Net;
using System.Text.Json;

namespace FootballManagementEngine;

/// <summary>Server-agnostic API application layer.</summary>
public sealed class GameApi
{
    public const string TeamsPath = "/api/teams";
    public const string GamePath = "/api/game";
    public const string SelectTeamPath = "/api/game/select-team";
    public const string FormationPath = "/api/game/formation";
    public const string SimulatePath = "/api/game/simulate";
    public const string FixturesPath = "/api/game/fixtures";
    public const string CompetitionsPath = "/api/game/competitions";
    public const string StandingsPath = "/api/game/standings";
    public const string PlayerStatsPath = "/api/game/player-stats";
    public const string SavePath = "/api/game/save";
    public const string SaveSlotsPath = "/api/game/save-slots";

    private readonly FootballGameEngine _game;
    public GameApi(FootballGameEngine game) => _game = game;

    public ApiResponse Handle(string method, string path, string? body = null)
    {
        method = method.Trim().ToUpperInvariant(); path = path.TrimEnd('/'); if (path.Length == 0) path = "/";
        try
        {
            return (method, path) switch
            {
                ("GET", TeamsPath) => Ok(new { teams = _game.State.Teams.Values.OrderBy(t => t.LeagueId).ThenBy(t => t.Name).Select(t => new TeamSummary(t.Id, t.Name, t.ShortName, t.LeagueId, t.Formation)).ToList() }),
                ("GET", GamePath) => Ok(new { playerTeamId = _game.State.PlayerTeamId, season = _game.State.Season, currentDateUtc = _game.State.CurrentDateUtc, playerTeam = _game.GetPlayerTeam() is { } team ? new TeamSummary(team.Id, team.Name, team.ShortName, team.LeagueId, team.Formation) : null }),
                ("POST", SelectTeamPath) => SelectTeam(body),
                ("POST", FormationPath) => SetFormation(body),
                ("POST", SimulatePath) => Simulate(body),
                ("GET", FixturesPath) => GetFixtures(),
                ("GET", CompetitionsPath) => GetCompetitions(),
                ("GET", StandingsPath) => GetStandings(),
                ("GET", PlayerStatsPath) => GetPlayerStats(),
                ("POST", SavePath) => Save(body),
                ("GET", SaveSlotsPath) => Ok(new { slots = _game.Persistence?.GetSlots() ?? Array.Empty<string>() }),
                _ => Error(HttpStatusCode.NotFound, "API endpoint not found.")
            };
        }
        catch (JsonException) { return Error(HttpStatusCode.BadRequest, "Invalid JSON request body."); }
        catch (ArgumentException ex) { return Error(HttpStatusCode.BadRequest, ex.Message); }
        catch (KeyNotFoundException ex) { return Error(HttpStatusCode.NotFound, ex.Message); }
        catch (InvalidOperationException ex) { return Error(HttpStatusCode.Conflict, ex.Message); }
        catch (FileNotFoundException ex) { return Error(HttpStatusCode.NotFound, ex.Message); }
    }

    private ApiResponse SelectTeam(string? body)
    {
        var request = JsonSerializer.Deserialize<SelectTeamRequest>(body ?? "", _game.JsonOptions) ?? throw new ArgumentException("Request body is required.");
        var team = _game.SelectPlayerTeam(request.TeamId);
        return Ok(new { success = true, playerTeamId = team.Id, playerTeam = new TeamSummary(team.Id, team.Name, team.ShortName, team.LeagueId, team.Formation) });
    }

    private ApiResponse GetFixtures() => Ok(new { fixtures = _game.Fixtures().Select(f => new { f.Id, f.CompetitionId, f.Round, f.DateUtc, f.HomeTeamId, f.AwayTeamId, f.IsPlayed, f.HomeGoals, f.AwayGoals, f.ExtraTimePlayed, f.HomePenalties, f.AwayPenalties, f.TieId }).ToList() });

    private ApiResponse GetCompetitions() => Ok(new { competitions = _game.State.Competitions.Values.Select(c => new { c.Id, c.Name, type = c.Type.ToString(), c.LeagueId, c.TeamIds, matchRules = c.MatchRules }).ToList() });

    private ApiResponse GetStandings()
    {
        var tables = _game.State.Leagues.Values.ToDictionary(l => l.Id, l => _game.GetLeagueTable(l.Id));
        return Ok(new { standings = tables });
    }

    private ApiResponse GetPlayerStats()
    {
        var stats = _game.State.PlayerStats.Values
            .Join(_game.State.Teams.Values.SelectMany(t => t.Players), s => s.PlayerId, p => p.Id, (s, p) => new { stats = s, player = p })
            .Select(x => new { x.stats.PlayerId, x.stats.TeamId, x.player.Name, x.player.Position, x.player.Injured, x.player.InjuryWeeks, x.player.SuspensionMatches, x.stats.Season, x.stats.Appearances, x.stats.Starts, x.stats.Minutes, x.stats.Goals, x.stats.Assists, x.stats.YellowCards, x.stats.RedCards, x.stats.CleanSheets, x.stats.Injuries })
            .OrderByDescending(x => x.Goals).ThenByDescending(x => x.Appearances).ToList();
        return Ok(new { playerStats = stats });
    }

    private ApiResponse Save(string? body)
    {
        var request = JsonSerializer.Deserialize<SaveGameRequest>(body ?? "{}", _game.JsonOptions) ?? new SaveGameRequest();
        _game.Save(request.Slot ?? "default");
        return Ok(new { success = true, slot = request.Slot ?? "default" });
    }

    private ApiResponse SetFormation(string? body)
    {
        var request = JsonSerializer.Deserialize<SetFormationRequest>(body ?? "", _game.JsonOptions) ?? throw new ArgumentException("Request body is required.");
        _game.SetFormation(request.TeamId, request.Formation);
        return Ok(new { success = true, teamId = request.TeamId, formation = request.Formation.ToString() });
    }

    private ApiResponse Simulate(string? body)
    {
        var request = JsonSerializer.Deserialize<SimulateMatchRequest>(body ?? "", _game.JsonOptions) ?? throw new ArgumentException("Request body is required.");
        var result = _game.SimulateFixture(request.FixtureId, new MatchSimulationOptions { DurationSeconds = request.DurationSeconds ?? 8, IncludeHighlights = request.IncludeHighlights ?? true, HighlightCount = request.HighlightCount ?? 10, MatchMinutes = request.MatchMinutes ?? 90 }, request.Seed);
        return Ok(new { result, fixture = _game.State.Fixtures.Single(f => f.Id == result.FixtureId), replayFixture = result.ReplayFixtureId == null ? null : _game.State.Fixtures.Single(f => f.Id == result.ReplayFixtureId) });
    }

    private ApiResponse Ok(object value) => new((int)HttpStatusCode.OK, JsonSerializer.Serialize(value, _game.JsonOptions));
    private static ApiResponse Error(HttpStatusCode status, string message) => new((int)status, JsonSerializer.Serialize(new { error = message }));
}

public sealed record ApiResponse(int StatusCode, string Body);
public sealed record SelectTeamRequest(string TeamId);
public sealed record TeamSummary(string Id, string Name, string ShortName, string LeagueId, Formation Formation);
public sealed record SetFormationRequest(string TeamId, Formation Formation);
public sealed record SimulateMatchRequest(string FixtureId, int? DurationSeconds = null, bool? IncludeHighlights = null, int? HighlightCount = null, int? MatchMinutes = null, int? Seed = null);
public sealed record SaveGameRequest(string? Slot = "default");
