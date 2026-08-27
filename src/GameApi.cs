using System.Net;
using System.Text.Json;

namespace FootballManagementEngine;

/// <summary>
/// Server-agnostic API application layer. It handles HTTP-like method/path/body
/// values but has no dependency on ASP.NET, Kestrel, HttpListener, or any web server.
/// Any host (ASP.NET, Node, Lambda, Azure Functions, a custom TCP server, etc.) can
/// map its request to HandleAsync and return the resulting status code/body.
/// </summary>
public sealed class GameApi
{
    public const string TeamsPath = "/api/teams";
    public const string GamePath = "/api/game";
    public const string SelectTeamPath = "/api/game/select-team";

    private readonly FootballGameEngine _game;

    public GameApi(FootballGameEngine game) => _game = game;

    public ApiResponse Handle(string method, string path, string? body = null)
    {
        method = method.Trim().ToUpperInvariant();
        path = path.TrimEnd('/');
        if (path.Length == 0) path = "/";

        try
        {
            return (method, path) switch
            {
                ("GET", TeamsPath) => Ok(new
                {
                    teams = _game.State.Teams.Values
                        .OrderBy(t => t.LeagueId)
                        .ThenBy(t => t.Name)
                        .Select(t => new TeamSummary(t.Id, t.Name, t.ShortName, t.LeagueId))
                        .ToList()
                }),

                ("GET", GamePath) => Ok(new
                {
                    playerTeamId = _game.State.PlayerTeamId,
                    playerTeam = _game.GetPlayerTeam() is { } team
                        ? new TeamSummary(team.Id, team.Name, team.ShortName, team.LeagueId)
                        : null
                }),

                ("POST", SelectTeamPath) => SelectTeam(body),
                _ => Error(HttpStatusCode.NotFound, "API endpoint not found.")
            };
        }
        catch (JsonException)
        {
            return Error(HttpStatusCode.BadRequest, "Invalid JSON request body.");
        }
        catch (ArgumentException ex)
        {
            return Error(HttpStatusCode.BadRequest, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Error(HttpStatusCode.NotFound, ex.Message);
        }
    }

    private ApiResponse SelectTeam(string? body)
    {
        var request = JsonSerializer.Deserialize<SelectTeamRequest>(body ?? "", _game.JsonOptions)
            ?? throw new ArgumentException("Request body is required.");

        var team = _game.SelectPlayerTeam(request.TeamId);
        return Ok(new
        {
            success = true,
            playerTeamId = team.Id,
            playerTeam = new TeamSummary(team.Id, team.Name, team.ShortName, team.LeagueId)
        });
    }

    private ApiResponse Ok(object value) => new(
        (int)HttpStatusCode.OK,
        JsonSerializer.Serialize(value, _game.JsonOptions));

    private static ApiResponse Error(HttpStatusCode status, string message) => new(
        (int)status,
        JsonSerializer.Serialize(new { error = message }));
}

public sealed record ApiResponse(int StatusCode, string Body);
public sealed record SelectTeamRequest(string TeamId);
public sealed record TeamSummary(string Id, string Name, string ShortName, string LeagueId);
