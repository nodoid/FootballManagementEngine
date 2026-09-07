namespace FootballManagementEngine.Demo;

/// <summary>One squad member, flattened for display.</summary>
public sealed record SquadMember(
    string PlayerId,
    string Name,
    Position Position,
    int Age,
    int Overall,
    decimal WeeklyWage,
    bool Injured,
    int InjuryWeeks,
    int Appearances,
    int Goals,
    int CleanSheets);

/// <summary>A fixture with both club names resolved, ready to put on screen.</summary>
public sealed record FixtureCard(
    string FixtureId,
    string CompetitionName,
    int Round,
    DateTime DateUtc,
    string HomeTeamId,
    string HomeName,
    string AwayTeamId,
    string AwayName,
    bool IsPlayed,
    int? HomeGoals,
    int? AwayGoals)
{
    public bool IsHome(string teamId) => HomeTeamId == teamId;
    public string Opponent(string teamId) => IsHome(teamId) ? AwayName : HomeName;
    public string Venue(string teamId) => IsHome(teamId) ? "H" : "A";
    public string Score => IsPlayed ? $"{HomeGoals}-{AwayGoals}" : "v";
}

/// <summary>
/// The application layer both demo front ends drive. It owns the engine, the season calendar
/// and the managed club, and exposes only what a screen needs, so the MAUI and MonoGame heads
/// share one set of rules and differ only in how they draw.
/// </summary>
public sealed class GameSession
{
    private readonly SeasonEngine _season;

    private GameSession(FootballGameEngine game)
    {
        Game = game;
        _season = new SeasonEngine(game);
    }

    public FootballGameEngine Game { get; }

    /// <summary>The club the player manages, or null before one is chosen.</summary>
    public Team? Club => Game.GetPlayerTeam();

    public int Season => Game.State.Season;
    public DateTime CurrentDateUtc => Game.State.CurrentDateUtc;

    /// <summary>
    /// Opens the demo save in <paramref name="databasePath"/>, or builds a fresh English league
    /// pyramid and schedules a full season if there is nothing saved yet.
    /// </summary>
    /// <remarks>
    /// The slot is deliberately the engine's default: <see cref="FootballGameEngine"/> auto-saves
    /// to "default", so using any other slot here would strand every later save in a slot the
    /// demo never reads back.
    /// </remarks>
    public static GameSession Open(string databasePath, string slot = "default")
    {
        var game = UkDatabase.Create(databasePath, loadExisting: true, autoSave: true, slot);
        var session = new GameSession(game);

        // A brand new world has no calendar yet; a resumed save must never be regenerated.
        if (game.State.Fixtures.Count == 0)
        {
            foreach (var team in game.State.Teams.Values) PickBestEleven(team);
            session._season.GenerateDomesticSeason();
            session._season.GenerateFaCup();

            // The engine's calendar starts on 1 July, a month before anyone kicks a ball. Open
            // on the first day of the season instead, so both demos start on matchday one.
            if (game.State.Fixtures.Count > 0)
                game.State.CurrentDateUtc = game.State.Fixtures.Min(f => f.DateUtc);

            game.Save(slot);
        }

        return session;
    }

    /// <summary>
    /// The engine fields the first eleven available players in squad order, so squad order *is*
    /// team selection. Sorting each club into a 4-4-2 of its best available players turns that
    /// into a sensible line-up instead of the seed data's goalkeeper-and-seven-defenders.
    /// </summary>
    private static void PickBestEleven(Team team)
    {
        var byPosition = team.Players
            .GroupBy(p => p.Position)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Overall).ToList());

        var eleven = new List<Player>();
        foreach (var (position, count) in new[]
                 {
                     (Position.GK, 1), (Position.DEF, 4), (Position.MID, 4), (Position.FWD, 2)
                 })
        {
            if (byPosition.TryGetValue(position, out var players))
                eleven.AddRange(players.Take(count));
        }

        var bench = team.Players
            .Except(eleven)
            .OrderBy(p => p.Position)
            .ThenByDescending(p => p.Overall);

        var ordered = eleven.Concat(bench).ToList();
        team.Players.Clear();
        team.Players.AddRange(ordered);
    }

    /// <summary>Every club, ordered by division then name - the club picker's data source.</summary>
    public IReadOnlyList<Team> Clubs =>
        Game.State.Teams.Values
            .OrderBy(t => Game.State.Leagues.TryGetValue(t.LeagueId, out var l) ? l.Level : int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<League> Divisions =>
        Game.State.Leagues.Values.OrderBy(l => l.Level).ToList();

    public string LeagueName(string leagueId) =>
        Game.State.Leagues.TryGetValue(leagueId, out var league) ? league.Name : leagueId;

    public void SelectClub(string teamId) => Game.SelectPlayerTeam(teamId);

    public void SetFormation(Formation formation)
    {
        if (Club is { } club) Game.SetFormation(club.Id, formation);
    }

    /// <summary>The managed club's league table, or an empty table before a club is chosen.</summary>
    public List<StandingRow> Table =>
        Club is { } club && Game.State.Leagues.ContainsKey(club.LeagueId)
            ? Game.GetLeagueTable(club.LeagueId)
            : [];

    public int TablePosition =>
        Club is { } club ? Table.FindIndex(r => r.TeamId == club.Id) + 1 : 0;

    /// <summary>The managed club's fixtures, oldest first.</summary>
    public IReadOnlyList<FixtureCard> Fixtures =>
        Club is { } club
            ? Game.Fixtures(teamId: club.Id).Select(ToCard).ToList()
            : [];

    /// <summary>The next unplayed fixture for the managed club.</summary>
    public FixtureCard? NextFixture =>
        Club is { } club
            ? Game.Fixtures(teamId: club.Id).Where(f => !f.IsPlayed).Select(ToCard).FirstOrDefault()
            : null;

    public IReadOnlyList<FixtureCard> RecentResults(int count = 5) =>
        Club is { } club
            ? Game.Fixtures(teamId: club.Id).Where(f => f.IsPlayed).TakeLast(count).Select(ToCard).ToList()
            : [];

    public IReadOnlyList<SquadMember> Squad =>
        Club is { } club
            ? club.Players
                .Select(p =>
                {
                    Game.State.PlayerStats.TryGetValue(p.Id, out var stats);
                    return new SquadMember(
                        p.Id, p.Name, p.Position, p.Age, p.Overall, p.WeeklyWage,
                        p.Injured, p.InjuryWeeks,
                        stats?.Appearances ?? 0, stats?.Goals ?? 0, stats?.CleanSheets ?? 0);
                })
                .OrderBy(p => p.Position)
                .ThenByDescending(p => p.Overall)
                .ToList()
            : [];

    /// <summary>
    /// Plays the managed club's next match. Every other fixture on the same day is played too,
    /// so the division's table stays honest rather than showing one club running away with it.
    /// </summary>
    public MatchResult? PlayNextMatch(MatchSimulationOptions? options = null, int? seed = null)
    {
        if (NextFixture is not { } next) return null;

        var competitionId = Game.State.Fixtures.Single(f => f.Id == next.FixtureId).CompetitionId;

        options ??= new MatchSimulationOptions { DurationSeconds = 0, HighlightCount = 8 };
        var result = Game.SimulateFixture(next.FixtureId, options, seed);

        var sameDay = Game.State.Fixtures
            .Where(f => !f.IsPlayed
                        && f.CompetitionId == competitionId
                        && f.DateUtc.Date == next.DateUtc.Date)
            .Select(f => f.Id)
            .ToList();

        foreach (var fixtureId in sameDay)
            Game.SimulateFixture(fixtureId, new MatchSimulationOptions { DurationSeconds = 0, IncludeHighlights = false });

        Game.State.CurrentDateUtc = next.DateUtc;
        Game.SaveIfConfigured();
        return result;
    }

    /// <summary>Moves the calendar on a week, paying wages and healing injuries.</summary>
    public void AdvanceWeek() => _season.ProcessWeek();

    /// <summary>Wipes the save so the demo can be restarted from the club picker.</summary>
    public void ResetSeason()
    {
        Game.State.PlayerTeamId = null;
        foreach (var fixture in Game.State.Fixtures)
        {
            fixture.IsPlayed = false;
            fixture.HomeGoals = null;
            fixture.AwayGoals = null;
            fixture.ExtraTimePlayed = false;
            fixture.HomePenalties = null;
            fixture.AwayPenalties = null;
        }
        Game.ResetSeasonPlayerStats();
        Game.SaveIfConfigured();
    }

    private FixtureCard ToCard(Fixture fixture) => new(
        fixture.Id,
        Game.State.Competitions.TryGetValue(fixture.CompetitionId, out var competition)
            ? competition.Name
            : fixture.CompetitionId,
        fixture.Round,
        fixture.DateUtc,
        fixture.HomeTeamId,
        Game.State.Teams.TryGetValue(fixture.HomeTeamId, out var home) ? home.Name : fixture.HomeTeamId,
        fixture.AwayTeamId,
        Game.State.Teams.TryGetValue(fixture.AwayTeamId, out var away) ? away.Name : fixture.AwayTeamId,
        fixture.IsPlayed,
        fixture.HomeGoals,
        fixture.AwayGoals);

    /// <summary>
    /// Where the demo save lives. Both heads write to the platform's private app data folder,
    /// which is writable and backed up on Android and iOS alike.
    /// </summary>
    public static string DefaultDatabasePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetTempPath();
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "football-demo.db");
    }
}
