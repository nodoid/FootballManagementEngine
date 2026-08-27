# Server-Agnostic API

## Purpose

`GameApi` provides an HTTP-style application API without implementing an HTTP server. It accepts a method, path and optional body and returns an `ApiResponse` containing a status code and JSON body.

```csharp
var api = new GameApi(game);
var response = api.Handle("GET", "/api/teams");
```

A host adapter is responsible for mapping its incoming request and outgoing response to this contract.

## Endpoints

### `GET /api/teams`

Returns all teams that can be selected by the user.

Example response:

```json
{
  "teams": [
    {
      "id": "ARS",
      "name": "Arsenal",
      "shortName": "ARS",
      "leagueId": "PL"
    },
    {
      "id": "AVL",
      "name": "Aston Villa",
      "shortName": "AVL",
      "leagueId": "PL"
    }
  ]
}
```

Teams are ordered by league ID and then team name.

### `POST /api/game/select-team`

Sets the team controlled by the human manager.

Request:

```http
Content-Type: application/json

{
  "teamId": "ARS"
}
```

Successful response:

```json
{
  "success": true,
  "playerTeamId": "ARS",
  "playerTeam": {
    "id": "ARS",
    "name": "Arsenal",
    "shortName": "ARS",
    "leagueId": "PL"
  }
}
```

The endpoint validates that the supplied team exists. Invalid JSON or an invalid team ID produces an error response rather than changing the game state.

### `GET /api/game`

Returns the current player-team selection.

Example:

```json
{
  "playerTeamId": "ARS",
  "playerTeam": {
    "id": "ARS",
    "name": "Arsenal",
    "shortName": "ARS",
    "leagueId": "PL"
  }
}
```

Before a selection is made:

```json
{
  "playerTeamId": null,
  "playerTeam": null
}
```

## Status codes

| Status | Meaning |
|---:|---|
| 200 | Request completed successfully |
| 400 | Invalid JSON or invalid request data |
| 404 | Endpoint or referenced team was not found |

Error bodies use this shape:

```json
{
  "error": "message"
}
```

## Host integration

The API layer is intentionally server-agnostic. A server adapter should perform only transport concerns:

1. Read the incoming HTTP request.
2. Pass the HTTP method, path and body to `GameApi.Handle`.
3. Set the transport response status from `ApiResponse.StatusCode`.
4. Set the response body from `ApiResponse.Body`.
5. Set `Content-Type: application/json` for the JSON response.

Example adapter pseudocode:

```csharp
var response = api.Handle(
    request.Method,
    request.Path,
    request.Body);

return new HostResponse
{
    StatusCode = response.StatusCode,
    ContentType = "application/json",
    Body = response.Body
};
```

`GameApi` itself does not depend on ASP.NET Core, Kestrel, `HttpListener`, sockets, or another server implementation.

## Client flow

A typical client should:

1. Call `GET /api/teams`.
2. Display the returned teams in a selection control.
3. Send the selected team's ID to `POST /api/game/select-team`.
4. Call `GET /api/game` when it needs to restore/display the current selection.

Example JavaScript:

```javascript
const response = await fetch('/api/teams');
const { teams } = await response.json();

const team = teams.find(t => t.id === 'ARS');

await fetch('/api/game/select-team', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ teamId: team.id })
});
```

## .NET target

This release targets **.NET 10** (`net10.0`). The API contract itself is independent of the transport/server framework.
