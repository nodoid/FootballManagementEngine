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

## Software License
## API documentation

This code is released under the ultra liberal (and some may say silly) DILLIGAF license. If you use it and it makes your computer explode - DILLIGAF. On the other hands, if you use it and it makes you a multi-millionaire, then again DILLIGAF.
See [`docs/API.md`](docs/API.md) for the complete endpoint contract, request/response examples, host integration guidance, and error behaviour.

## Data note

The club/player data is an illustrative generated starter database and is not intended to represent current official squads. A commercial product should use appropriately licensed football data.

### List fixtures

```http
GET /api/game/fixtures
```

Returns fixtures including their IDs, competition, teams, result, extra-time and
penalty information. Use the fixture ID with the simulation endpoint.

## Formations, simulation speed and highlights

Each team now has a configurable `Formation`. Supported formations are:

- `F442`
- `F433`
- `F4231`
- `F352`
- `F343`
- `F451`
- `F4141`
- `F532`
- `F541`
- `F41212`

Formation is part of the saved `Team` state and changes the simulated attacking,
midfield and defensive balance, so it can change the probabilities of winning,
drawing and losing rather than only changing presentation.

### Change a formation

```http
POST /api/game/formation
Content-Type: application/json

{
  "teamId": "ARS",
  "formation": "F433"
}
```

### Simulate a match

```http
POST /api/game/simulate
Content-Type: application/json

{
  "fixtureId": "fixture-id",
  "durationSeconds": 10,
  "includeHighlights": true,
  "highlightCount": 12,
  "matchMinutes": 90,
  "seed": 12345
}
```

`durationSeconds` is the intended client/UI playback duration; the server-side
simulation remains immediate. `matchMinutes` controls the simulated match length
(default 90), while extra time adds a further 30 simulated minutes when enabled. The result contains timestamped highlights, goals,
extra-time information and penalty-shootout information.

### Cup match resolution

Knockout competitions have `CompetitionMatchRules`:

- `ReplayAllowed`
- `MaxReplays`
- `ExtraTimeAllowed`
- `PenaltiesAllowed`

The supplied English database configures the FA Cup with one replay followed by
extra time/penalties if the replay is also drawn. The Carabao Cup and EFL Trophy
go directly to extra time and penalties. These are configuration values, so a
host/game can change them without changing the simulator.

A drawn replayable match is automatically marked as played and a new replay
fixture is added seven days later at the opposite venue. If no replay is
permitted (or the replay limit has been reached), a knockout draw proceeds to
extra time and then penalties when those rules are enabled.

## SQLite save games

The engine now supports optional SQLite persistence through `Microsoft.Data.Sqlite`.
The database stores the complete `GameState` plus queryable snapshots for league tables,
player season statistics and competition/cup configuration. Because the complete state is
stored as JSON as well, fixtures, replay ties, formations, injuries, suspensions, budgets,
transfers, news, dates and future state fields survive a restart without requiring a schema
change for every new game-state property.

### Start a new database-backed game

```bash
dotnet run -- --db football-management.db
```

### Resume a saved game

```bash
dotnet run -- --db football-management.db --load
```

Use a different save slot:

```bash
dotnet run -- --db football-management.db --load --slot career-2
```

### Save from the API

`POST /api/game/save`

```json
{ "slot": "default" }
```

### Query persisted game information

- `GET /api/game/standings` - all current league tables
- `GET /api/game/player-stats` - player appearances, minutes, goals, assists, cards, clean sheets and current injury/suspension state
- `GET /api/game/competitions` - league/cup/european competition configuration and teams
- `GET /api/game/fixtures` - fixtures and completed cup results, including replays, extra time and penalties
- `GET /api/game/save-slots` - available save slots

### Automatic saving

A database-backed game created with `autoSave: true` automatically saves important state
changes such as team selection, formation changes, completed matches and weekly progression.
A host can also call `game.Save("slot-name")` at safe checkpoints (for example when the app
is backgrounded or closed).

Injuries and suspensions are part of each `Player` and therefore survive a save/load cycle.
Player season aggregates are stored in `GameState.PlayerStats` and in the SQLite `PlayerStats`
table. League standings are recalculated from persisted fixtures and also materialised in the
SQLite `LeagueStandings` table. Cup competitions, rules, ties, replays, extra time and penalty
results are retained in the complete saved state and indexed competition snapshot.

## Tests

An NUnit suite covering the engine lives in `tests/FootballManagementEngine.Tests`:

```bash
dotnet test FootballManagementEngine.slnx
```

## Demo apps

Two mobile front ends over the same engine live in `demos/`, in their own solution so the
main build stays fast:

- **.NET MAUI** (`Android`, `iOS`, `Mac Catalyst`, `Windows`) - tabbed app: club picker, fixtures with
  live match commentary, league table and squad.
- **MonoGame** (`Android`, `iOS`) - a touch-driven game rendering the same season, with a
  match screen that replays the engine's highlights against a running clock.

```bash
dotnet build FootballManagementEngine.Demos.slnx
```

See [demos/README.md](demos/README.md) for how to run each one and why the demos reference
`FootballManagementEngine.Demo.Core` rather than this project directly.

## License

Released under the DILLIGAF license. 