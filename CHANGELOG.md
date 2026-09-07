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

## Transfer market

- Added `TransferMarket`: player valuations from ability, age and remaining potential; asking
  prices, with a premium for anyone their club has not listed; transfer windows (summer and
  January); and the wage a player expects.
- Added `FootballGameEngine.TransferMarketListings`, `QuoteFor` and `Bid`. A bid is validated
  in full - window, asking price, wages, budget and both squad sizes - before any money moves,
  so a rejected bid leaves both clubs untouched. Accepted deals rebuild both selections and are
  reported in the news feed.
- Added `ListForTransfer` / `WithdrawFromTransferList`, persisted in `GameState`. Nothing lists
  a player automatically: a squad member reaches the market only when their manager puts them
  there.
- Money is formatted in pounds regardless of the device's locale.

## Player state, substitutes and in-match injuries

- Added `PlayerState` (available, selected, substitute, injured, suspended) and the
  `Player.Selected`/`Player.Substitute` flags behind it.
- Squad order is now team selection: `FootballGameEngine.StartingEleven` and `Substitutes`
  are the single definition of who plays, used by the simulator, the season statistics and
  the demos alike. Selection is refreshed whenever availability changes.
- Players can now be injured during a match. The simulator raises an `Injury` event naming a
  starter, and the engine turns it into a real lay-off of one to four weeks.
- `MatchHighlight` gained a `PlayerId`, so an event can identify who it is about.
- `MatchSimulationOptions.InjuryChance` controls how often this happens (zero disables it).
- Every club is now seeded with two goalkeepers, so there is cover in goal and a keeper
  available for the bench.
- Cover for an unavailable player is now like for like: the first eleven in squad order are
  the intended side, and anyone missing is replaced by an available player of the same
  position where the squad allows it, falling back to the next available player otherwise.
  An injured defender no longer pulls the reserve goalkeeper into the back four.

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
