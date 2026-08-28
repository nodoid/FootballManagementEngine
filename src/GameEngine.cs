using System.Text.Json;
using System.Text.Json.Serialization;

namespace FootballManagementEngine;

public sealed class FootballGameEngine
{
    public GameState State { get; }
    public GamePersistence? Persistence { get; }
    public bool AutoSave { get; }

    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FootballGameEngine(GameState? state = null, GamePersistence? persistence = null, bool autoSave = false)
    {
        State = state ?? new GameState();
        Persistence = persistence;
        AutoSave = autoSave;
        InitialisePlayerStats();
    }

    public void Save(string slot = "default") =>
        (Persistence ?? throw new InvalidOperationException("No SQLite persistence has been configured.")).Save(this, slot);

    public bool SaveIfConfigured(string slot = "default")
    {
        if (Persistence == null) return false;
        Persistence.Save(this, slot);
        return true;
    }

    private void AutoSaveIfEnabled()
    {
        if (AutoSave && Persistence != null) Persistence.Save(this);
    }

    private void InitialisePlayerStats()
    {
        foreach (var team in State.Teams.Values)
        foreach (var player in team.Players)
        {
            if (!State.PlayerStats.ContainsKey(player.Id))
                State.PlayerStats[player.Id] = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
        }
    }

    public void AddTeam(Team team)
    {
        State.Teams[team.Id] = team;
        foreach (var player in team.Players)
            if (!State.PlayerStats.ContainsKey(player.Id))
                State.PlayerStats[player.Id] = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
    }

    /// <summary>
    /// Selects the club managed by the human player.
    /// </summary>
    public Team SelectPlayerTeam(string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
            throw new ArgumentException("Team ID is required.", nameof(teamId));

        if (!State.Teams.TryGetValue(teamId, out var team))
            throw new KeyNotFoundException($"Team '{teamId}' not found.");

        State.PlayerTeamId = team.Id;
        AutoSaveIfEnabled();
        return team;
    }

    public Team? GetPlayerTeam() =>
        State.PlayerTeamId != null && State.Teams.TryGetValue(State.PlayerTeamId, out var team)
            ? team
            : null;
    public void AddLeague(League league) => State.Leagues[league.Id] = league;
    public void AddCompetition(Competition competition) =>
        State.Competitions[competition.Id] = competition;

    public void ResetSeasonPlayerStats()
    {
        State.PlayerStats.Clear();
        InitialisePlayerStats();
        AutoSaveIfEnabled();
    }

    public void SetPlayerInjury(string playerId, int weeks)
    {
        var player = State.Teams.Values.SelectMany(t => t.Players).SingleOrDefault(p => p.Id == playerId)
            ?? throw new KeyNotFoundException($"Player '{playerId}' not found.");
        if (weeks < 0) throw new ArgumentOutOfRangeException(nameof(weeks));
        player.Injured = weeks > 0;
        player.InjuryWeeks = weeks;
        if (weeks > 0 && State.PlayerStats.TryGetValue(player.Id, out var stats)) stats.Injuries++;
        AutoSaveIfEnabled();
    }

    public void SetFormation(string teamId, Formation formation)
    {
        if (!State.Teams.TryGetValue(teamId, out var team))
            throw new KeyNotFoundException($"Team '{teamId}' not found.");
        team.Formation = formation;
        AutoSaveIfEnabled();
    }

    public Formation GetFormation(string teamId) =>
        State.Teams.TryGetValue(teamId, out var team)
            ? team.Formation
            : throw new KeyNotFoundException($"Team '{teamId}' not found.");

    /// <summary>
    /// Simulates and applies a fixture. Knockout rules are taken from the competition.
    /// A drawn replayable tie creates the next fixture; otherwise extra time and
    /// penalties are resolved according to the competition rules.
    /// </summary>
    public MatchResult SimulateFixture(
        string fixtureId,
        MatchSimulationOptions? options = null,
        int? seed = null)
    {
        var fixture = State.Fixtures.SingleOrDefault(f => f.Id == fixtureId)
            ?? throw new KeyNotFoundException($"Fixture '{fixtureId}' not found.");
        if (fixture.IsPlayed)
            throw new InvalidOperationException("Fixture already has a result.");

        if (!State.Teams.TryGetValue(fixture.HomeTeamId, out var home) ||
            !State.Teams.TryGetValue(fixture.AwayTeamId, out var away))
            throw new KeyNotFoundException("Fixture contains an unknown team.");

        if (!State.Competitions.TryGetValue(fixture.CompetitionId, out var competition))
            throw new KeyNotFoundException($"Competition '{fixture.CompetitionId}' not found.");

        var simulator = new MatchSimulator(seed ?? Environment.TickCount);
        var result = simulator.Simulate(fixture, home, away, competition.MatchRules, options);

        if (result.HomeGoals == result.AwayGoals &&
            competition.Type != CompetitionType.League &&
            competition.MatchRules.ReplayAllowed &&
            ReplayCount(fixture) < competition.MatchRules.MaxReplays)
        {
            var replay = new Fixture
            {
                CompetitionId = fixture.CompetitionId,
                Round = fixture.Round,
                DateUtc = fixture.DateUtc.AddDays(7),
                HomeTeamId = fixture.AwayTeamId,
                AwayTeamId = fixture.HomeTeamId,
                TieId = fixture.TieId ?? fixture.Id
            };

            State.Fixtures.Add(replay);
            result = new MatchResult
            {
                FixtureId = result.FixtureId,
                HomeGoals = result.HomeGoals,
                AwayGoals = result.AwayGoals,
                ExtraTime = false,
                DateUtc = result.DateUtc,
                DurationSeconds = result.DurationSeconds,
                Highlights = result.Highlights,
                ReplayRequired = true,
                ReplayFixtureId = replay.Id
            };
        }
        else if (result.HomeGoals == result.AwayGoals &&
                 competition.Type != CompetitionType.League)
        {
            result = ResolveKnockoutDraw(result, fixture, home, away, competition.MatchRules, options);
        }

        ApplyResult(result);
        return result;
    }

    private MatchResult ResolveKnockoutDraw(
        MatchResult result, Fixture fixture, Team home, Team away,
        CompetitionMatchRules rules, MatchSimulationOptions? options)
    {
        var hg = result.HomeGoals;
        var ag = result.AwayGoals;
        var extraTime = false;
        var highlights = result.Highlights.ToList();

        if (rules.ExtraTimeAllowed)
        {
            extraTime = true;
            var et = new MatchSimulator(Environment.TickCount + 17)
                .Simulate(
                    fixture, home, away,
                    new CompetitionMatchRules(),
                    new MatchSimulationOptions
                    {
                        DurationSeconds = 0,
                        IncludeHighlights = options?.IncludeHighlights ?? true,
                        HighlightCount = 2,
                        MatchMinutes = 30
                    });
            hg += et.HomeGoals;
            ag += et.AwayGoals;
            highlights.AddRange(et.Highlights.Select(h => new MatchHighlight
            {
                Minute = h.Minute + 90,
                TeamId = h.TeamId,
                Type = h.Type == MatchEventType.Goal ? MatchEventType.Goal : h.Type,
                Description = $"Extra time: {h.Description}"
            }));
        }

        int? hp = null, ap = null;
        if (hg == ag && rules.PenaltiesAllowed)
        {
            (hp, ap) = PenaltyShootout(home, away);
            highlights.Add(new MatchHighlight
            {
                Minute = 120,
                Type = MatchEventType.PenaltyShootout,
                Description = $"{home.ShortName} {hp}–{ap} {away.ShortName} on penalties."
            });
        }

        return new MatchResult
        {
            FixtureId = result.FixtureId,
            HomeGoals = hg,
            AwayGoals = ag,
            ExtraTime = extraTime,
            HomePenalties = hp,
            AwayPenalties = ap,
            DateUtc = result.DateUtc,
            DurationSeconds = result.DurationSeconds,
            Highlights = highlights.OrderBy(h => h.Minute).ToList()
        };
    }

    private static (int Home, int Away) PenaltyShootout(Team home, Team away)
    {
        var rng = new Random(HashCode.Combine(home.Id, away.Id, DateTime.UtcNow.Ticks));
        var h = 0; var a = 0;
        for (var i = 0; i < 5; i++)
        {
            if (PenaltyScored(home, rng)) h++;
            if (PenaltyScored(away, rng)) a++;
        }
        var round = 0;
        while (h == a && round++ < 20)
        {
            if (PenaltyScored(home, rng)) h++;
            if (PenaltyScored(away, rng)) a++;
        }
        if (h == a) h++; // deterministic safety fallback
        return (h, a);
    }

    private static bool PenaltyScored(Team team, Random rng)
    {
        var keeper = team.Players.Where(p => p.Position == Position.GK).Select(p => p.Goalkeeping).DefaultIfEmpty(60).Average();
        var shooting = team.Players.Where(p => p.Position == Position.FWD || p.Position == Position.MID)
            .Select(p => p.Shooting).DefaultIfEmpty(60).Average();
        var chance = Math.Clamp(0.76 + (shooting - keeper) / 500.0, 0.65, 0.90);
        return rng.NextDouble() < chance;
    }

    private int ReplayCount(Fixture fixture)
    {
        var tieId = fixture.TieId ?? fixture.Id;
        return State.Fixtures.Count(f =>
            f.Id != fixture.Id &&
            f.CompetitionId == fixture.CompetitionId &&
            f.TieId == tieId);
    }

    public void ApplyResult(MatchResult result)
    {
        if (result.HomeGoals < 0 || result.AwayGoals < 0)
            throw new ArgumentException("Goals cannot be negative.");

        var fixture = State.Fixtures.SingleOrDefault(f => f.Id == result.FixtureId)
            ?? throw new KeyNotFoundException($"Fixture '{result.FixtureId}' not found.");

        if (fixture.IsPlayed)
            throw new InvalidOperationException("Fixture already has a result.");

        fixture.IsPlayed = true;
        fixture.HomeGoals = result.HomeGoals;
        fixture.AwayGoals = result.AwayGoals;
        fixture.ExtraTimePlayed = result.ExtraTime;
        fixture.HomePenalties = result.HomePenalties;
        fixture.AwayPenalties = result.AwayPenalties;
        UpdatePlayerStats(fixture, result);

        if (result.DateUtc.HasValue)
            fixture.DateUtc = result.DateUtc.Value;

        AutoSaveIfEnabled();
    }

    private void UpdatePlayerStats(Fixture fixture, MatchResult result)
    {
        if (!State.Teams.TryGetValue(fixture.HomeTeamId, out var home) || !State.Teams.TryGetValue(fixture.AwayTeamId, out var away)) return;
        UpdateTeamPlayerStats(home, result.HomeGoals, result.Highlights, fixture.HomeGoals.GetValueOrDefault(), fixture.AwayGoals.GetValueOrDefault());
        UpdateTeamPlayerStats(away, result.AwayGoals, result.Highlights, fixture.AwayGoals.GetValueOrDefault(), fixture.HomeGoals.GetValueOrDefault());
    }

    private void UpdateTeamPlayerStats(Team team, int goals, IReadOnlyList<MatchHighlight> highlights, int teamGoals, int opponentGoals)
    {
        var available = team.Players.Where(p => !p.Injured && p.SuspensionMatches == 0).ToList();
        if (available.Count == 0) return;
        var starters = available.Take(Math.Min(11, available.Count)).ToList();
        foreach (var player in starters)
        {
            if (!State.PlayerStats.TryGetValue(player.Id, out var stats))
                State.PlayerStats[player.Id] = stats = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
            stats.Appearances++; stats.Starts++; stats.Minutes += 90;
            if (teamGoals == 0) stats.CleanSheets++;
        }

        var goalHighlights = highlights.Where(h => h.Type == MatchEventType.Goal && h.TeamId == team.Id).ToList();
        for (var i = 0; i < Math.Min(goals, goalHighlights.Count); i++)
        {
            var player = starters[i % starters.Count];
            State.PlayerStats[player.Id].Goals++;
        }
        foreach (var card in highlights.Where(h => h.TeamId == team.Id && h.Type == MatchEventType.YellowCard))
        {
            var player = starters[Math.Abs(card.Minute) % starters.Count];
            State.PlayerStats[player.Id].YellowCards++;
        }
    }

    public void ApplyResultsJson(string json)
    {
        var results = JsonSerializer.Deserialize<List<MatchResult>>(json, JsonOptions)
            ?? throw new ArgumentException("Invalid results JSON.");

        foreach (var result in results)
            ApplyResult(result);
    }

    public List<StandingRow> GetLeagueTable(string leagueId)
    {
        var league = State.Leagues[leagueId];
        var competitionIds = State.Competitions.Values
            .Where(c => c.Type == CompetitionType.League && c.LeagueId == leagueId)
            .Select(c => c.Id)
            .ToHashSet();

        return LeagueTable.Build(
            league, State.Teams,
            State.Fixtures.Where(f => competitionIds.Contains(f.CompetitionId)));
    }

    public IEnumerable<Fixture> Fixtures(
        string? competitionId = null,
        string? teamId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        return State.Fixtures
            .Where(f => competitionId == null || f.CompetitionId == competitionId)
            .Where(f => teamId == null || f.HomeTeamId == teamId || f.AwayTeamId == teamId)
            .Where(f => fromUtc == null || f.DateUtc >= fromUtc)
            .Where(f => toUtc == null || f.DateUtc <= toUtc)
            .OrderBy(f => f.DateUtc);
    }

    public string ExportState() => JsonSerializer.Serialize(State, JsonOptions);

    public static FootballGameEngine ImportState(string json, GamePersistence? persistence = null, bool autoSave = false)
    {
        var state = JsonSerializer.Deserialize<GameState>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            }) ?? throw new ArgumentException("Invalid save game.");

        return new FootballGameEngine(state, persistence, autoSave);
    }
}
