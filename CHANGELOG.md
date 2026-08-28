# Changelog

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
