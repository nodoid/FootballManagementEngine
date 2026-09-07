# Demos

Two playable front ends over the same engine: a **.NET MAUI** app and a **MonoGame** game.
Both let you take charge of any club in the English pyramid, play matches, and watch the
league table move.

## Layout

| Project | Target | What it is |
| --- | --- | --- |
| `FootballManagementEngine.Demo.Core` | `net10.0` | The engine compiled as a library, plus `GameSession` — the app logic both front ends drive |
| `FootballManagementEngine.Maui` | android, ios, maccatalyst, windows | Tabbed MAUI app: Club, Fixtures, Table, Squad |
| `FootballManagementEngine.MonoGame.Shared` | shared source | The MonoGame screens, bitmap font and touch UI |
| `FootballManagementEngine.MonoGame.Android` | `net10.0-android` | MonoGame Android head |
| `FootballManagementEngine.MonoGame.iOS` | `net10.0-ios` | MonoGame iOS head |

The demos live in their own solution so the main one stays fast:

```bash
dotnet build FootballManagementEngine.Demos.slnx   # demos
dotnet test  FootballManagementEngine.slnx         # engine + 402 tests
```

## Why `Demo.Core` exists

`FootballManagementEngine.csproj` is an `Exe`, and a self-contained mobile head cannot
reference a non self-contained executable (`NETSDK1150`). `Demo.Core` compiles the same
files from `src/` into a library — `Program.cs` excluded, since that is the console app's
entry point. Nothing in `src/` is duplicated or forked; if you ever split the engine into
its own library, delete `Demo.Core` and reference that instead.

## Running them

```bash
# MAUI - easiest to look at, runs straight on the Mac
dotnet build demos/FootballManagementEngine.Maui/FootballManagementEngine.Maui.csproj \
  -f net10.0-maccatalyst -t:Run

# MAUI on Windows (run this on Windows; the target is not offered on macOS or Linux)
dotnet build demos/FootballManagementEngine.Maui/FootballManagementEngine.Maui.csproj \
  -f net10.0-windows10.0.19041.0 -t:Run

# MAUI on a connected Android device or running emulator
dotnet build demos/FootballManagementEngine.Maui/FootballManagementEngine.Maui.csproj \
  -f net10.0-android -t:Run

# MonoGame on Android
dotnet build demos/FootballManagementEngine.MonoGame.Android/FootballManagementEngine.MonoGame.Android.csproj \
  -t:Run

# MonoGame on iOS - open the project in Rider/Xcode to pick a simulator or device
dotnet build demos/FootballManagementEngine.MonoGame.iOS/FootballManagementEngine.MonoGame.iOS.csproj
```

iOS device builds need a provisioning profile and signing identity; the simulator does not.

The Windows target is added only when the build is running on Windows, which is what the
`IsOSPlatform('windows')` condition in the csproj guards - WinUI tooling cannot produce a
Windows app from macOS or Linux. It is packaged as an unpackaged app (`WindowsPackageType`
is `None`), so it runs with a plain `-t:Run` and needs no signing or Store identity.

On Windows the project still lists the Android, iOS and Mac Catalyst targets, and restore
evaluates every one of them, so a Windows-only machine needs those workloads present even
to build the Windows head. Install what the project asks for first:

```bash
dotnet workload restore demos/FootballManagementEngine.Maui/FootballManagementEngine.Maui.csproj
```

Without it the build stops at `NETSDK1147: the following workloads must be installed: android`.

## What the demos do

On first launch the session builds the full pyramid — 5 divisions, 116 clubs, 2,552
players — schedules a double round robin plus the FA Cup first round, and writes it to
SQLite in the app's private storage. Later launches resume that save.

Pick a club and you get:

- **Fixtures** — the season calendar, and a *Play match* button. The engine returns the
  whole match at once; both demos replay its highlights against a clock so it reads like a
  match unfolding. Every other fixture that day is simulated too, so the table stays honest.
- **Table** — the live division table, your club highlighted.
- **Squad** — the matchday squad in selection order: the starting eleven first (ticked in the
  *State* column), then the four substitutes (`S`), then the reserves, with injured players
  (`I`) pushed to the bottom. Appearances, goals and ratings per player, plus the wage bill.
- **Injuries and substitutions** — a player can limp off mid-match. The clock stops on the
  minute it happens, the bench is offered, and play resumes to full time once a replacement
  is named. Squad order *is* team selection, so the substitute simply takes the injured
  player's place and the engine re-reads the eleven.
- **Club** — the formation control and *Advance a week*, which pays wages and heals injuries.

Both demos open on a title card, then the club picker. Picking a club is a one-way door: the
picker is gone afterwards, because a manager stays at their club. A new game starts on the
opening day of the season rather than the engine's default 1 July.

## Notes for the MonoGame demo

- **No content pipeline.** Text uses a 5x7 bitmap font written as ASCII art in
  `PixelFont.cs` and baked into one texture at load, so there is no MGCB tool, no `.xnb`
  build step and no font file to license or ship. The MAUI splash screen is generated from
  the same glyph data, so both apps share one wordmark.
- **Fixed virtual canvas.** Everything is laid out in 360x640 and scaled to fit the device,
  so the layout is identical on every phone and tablet.
- **Shared as source, not as a library.** MonoGame ships a different framework assembly per
  platform, so the screens must compile against each one.

## Trimming

Both mobile heads root `FootballManagementEngine.Demo.Core` and `Microsoft.Data.Sqlite` for
the linker. The engine saves state with reflection-based `System.Text.Json`; without those
roots the app builds and then fails to save on device.
