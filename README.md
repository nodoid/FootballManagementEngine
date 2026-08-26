# Football Management Engine — Full Starter

.NET 10 C# football management simulation engine.

## What is included

### English pyramid
- Premier League
- Championship
- League One
- League Two
- National League

The seed database contains English club names and generated English player names.

### Competitions
- League seasons
- FA Cup
- Carabao Cup
- EFL Trophy
- Champions League
- Europa League
- Conference League
- European league-phase fixtures

### Core engine
- Fixture generation
- Results through JSON
- Automatic league tables
- Promotion/relegation
- Match simulation
- Player attributes
- Injuries and suspensions fields
- Transfers and contracts
- Club finances
- Weekly finance/player processing
- Complete JSON save/load

## Build and run

```bash
dotnet build
dotnet run
```

## Result API format

```json
[
  {
    "fixtureId": "fixture-id",
    "homeGoals": 2,
    "awayGoals": 1,
    "extraTime": false,
    "homePenalties": null,
    "awayPenalties": null
  }
]
```

The same JSON can be supplied to:

```csharp
game.ApplyResultsJson(json);
```

## Architecture

- `Domain.cs` — state model
- `FixtureGenerator.cs` — league/cup/European scheduling
- `LeagueTable.cs` — standings
- `MatchSimulator.cs` — basic match engine
- `TransferEngine.cs` — transfers and weekly wages
- `SeasonEngine.cs` — season generation and rollover operations
- `GameEngine.cs` — main application service
- `UkDatabase.cs` — English football seed data
- `Program.cs` — executable example

## Important production note

The club/player data is intentionally a generated starter database rather than a claim to contain the current official squads. For a commercial game, use appropriately licensed football data.

## Software License

This code is released under the ultra liberal (and some may say silly) DILLIGAF license. If you use it and it makes your computer explode - DILLIGAF. On the other hands, if you use it and it makes you a multi-millionaire, then again DILLIGAF.
