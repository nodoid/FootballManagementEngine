# Football Management Engine

A server-agnostic C# football management simulation engine targeting **.NET 10**.

## Requirements

- .NET 10 SDK

Verify the SDK with:

```powershell
dotnet --version
```

The project targets `net10.0` and does not reference ASP.NET Core, Kestrel, `HttpListener`, or any other HTTP server implementation.

## Run from the project root

After extracting the archive, open PowerShell in the extracted directory and run:

```powershell
dotnet restore
dotnet build
dotnet run
```

You can also use the supplied launchers:

```powershell
.\run.ps1
```

On Windows CMD:

```bat
run.bat
```

On Linux/macOS:

```bash
./run.sh
```

## User team selection

The application exposes a server-agnostic API through `src/GameApi.cs`. The user can retrieve the selectable team list, select a team, and retrieve the current selection.

### List teams

```http
GET /api/teams
```

Response:

```json
{
  "teams": [
    {
      "id": "ARS",
      "name": "Arsenal",
      "shortName": "ARS",
      "leagueId": "PL"
    }
  ]
}
```

### Select a team

```http
POST /api/game/select-team
Content-Type: application/json

{
  "teamId": "ARS"
}
```

A successful response is HTTP-style status `200` and contains the selected team.

### Get the current selection

```http
GET /api/game
```

Response:

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

If no team has been selected, `playerTeamId` and `playerTeam` are `null`.

## Calling the API from a host

`GameApi` deliberately does not open a network port. A host supplies the HTTP method, path, and optional request body and translates the returned `ApiResponse` into its own server response.

```csharp
var response = api.Handle(
    request.Method,
    request.Path,
    request.Body);

// Host-specific response mapping:
// status = response.StatusCode
// body   = response.Body
```

This keeps the game/application layer independent of the server technology. The same API can be hosted by ASP.NET Core, a different .NET web framework, a serverless adapter, a custom server, or called directly by a client/test harness without changing `GameApi`.

## JavaScript example

```javascript
const teamsResponse = await fetch('/api/teams');
const { teams } = await teamsResponse.json();

const selectedTeam = teams[0];

const selectionResponse = await fetch('/api/game/select-team', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ teamId: selectedTeam.id })
});

const selection = await selectionResponse.json();
console.log(selection);
```

## Architecture

- `src/Domain.cs` — game state, teams, players, fixtures and results
- `src/FixtureGenerator.cs` — fixture generation
- `src/LeagueTable.cs` — league standings
- `src/MatchSimulator.cs` — match simulation
- `src/TransferEngine.cs` — transfers and weekly wages
- `src/SeasonEngine.cs` — season generation and rollover
- `src/GameEngine.cs` — main application/game service, including team selection and JSON save/load
- `src/GameApi.cs` — server-agnostic API/application boundary
- `src/UkDatabase.cs` — English football seed data
- `src/Program.cs` — console example showing the API calls

## State and persistence

The selected team is stored in `GameState.PlayerTeamId`, so it is included in the normal JSON save/load state handled by the game engine.

## API documentation

See [`docs/API.md`](docs/API.md) for the complete endpoint contract, request/response examples, host integration guidance, and error behaviour.

## Data note

The club/player data is an illustrative generated starter database and is not intended to represent current official squads. A commercial product should use appropriately licensed football data.
