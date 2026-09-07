# Changelog

## Test suite, engine fixes and demo apps

- Added an NUnit test suite (`tests/FootballManagementEngine.Tests`) covering the domain,
  fixture generation, league tables, match simulation, transfers, the game and season
  engines, SQLite persistence, the API layer and end-to-end game flows.
- Fixed clean sheets being credited to the side that failed to score instead of the side
  that conceded nothing.
- Fixed goal attribution: goals went round-robin from the first player in the squad, which
  made the goalkeeper every club's top scorer. Goals are now credited to attacking players,
  weighted by position, and chosen by the goal's minute so attribution stays deterministic.
- Fixed relegated clubs falling two divisions in one pass: every league table is now
  snapshotted before any club moves.
- Balanced the promotion and relegation places so divisions keep their size across seasons
  (League One now promotes 3, the National League 2).
- Added .NET MAUI (Android/iOS/Mac Catalyst/Windows) and MonoGame (Android/iOS) demo apps under
  `demos/`, sharing a `GameSession` application layer over the engine.

## Formation and match simulation update

- Added configurable formations: 4-4-2, 4-3-3, 4-2-3-1, 3-5-2, 3-4-3,
  4-5-1, 4-1-4-1, 5-3-2, 5-4-1 and 4-1-2-1-2.
- Formation now affects simulated attacking, midfield and defensive strength,
  changing win/draw/loss probabilities.
- Added match simulation options for client playback duration, regulation
  match length and highlight generation.
- Added timestamped match highlights for chances, saves, misses, cards and goals.
- Added configurable knockout rules for replays, extra time and penalties.
- Drawn replayable cup fixtures automatically create a replay fixture seven days
  later at the opposite venue.
- Added API endpoints for changing formations, listing fixtures and simulating
  fixtures.
- Added penalty shootout resolution.

- Added SQLite save/load support with selectable save slots.
- Persisted complete game state, league tables, player statistics, injuries/suspensions, competitions and fixtures.
- Added automatic saving for key game mutations and weekly progression.
- Added API endpoints for standings, player stats, competitions, save slots and manual saves.
- Startup can optionally resume an existing SQLite save without regenerating fixtures or cups.
