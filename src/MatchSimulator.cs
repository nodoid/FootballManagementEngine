namespace FootballManagementEngine;

public sealed class MatchSimulationOptions
{
    /// <summary>Real-time duration of the simulation for a client/UI. It does not slow the engine.</summary>
    public int DurationSeconds { get; init; } = 8;
    /// <summary>Whether event highlights are returned.</summary>
    public bool IncludeHighlights { get; init; } = true;
    /// <summary>Approximate number of highlights generated per 90 minutes.</summary>
    public int HighlightCount { get; init; } = 10;
    public int MatchMinutes { get; init; } = 90;
}

public sealed class MatchSimulator
{
    private readonly Random _rng;

    public MatchSimulator(int seed = 12345) => _rng = new Random(seed);

    public MatchResult Simulate(
        Fixture fixture,
        Team home,
        Team away,
        CompetitionMatchRules? rules = null,
        MatchSimulationOptions? options = null)
    {
        options ??= new MatchSimulationOptions();
        rules ??= new CompetitionMatchRules();

        var homeStrength = TeamStrength(home);
        var awayStrength = TeamStrength(away);

        // Formation changes the attacking/defensive balance. It is deliberately
        // applied to xG rather than simply adding points to the stronger team.
        var homeTactics = FormationEffects(home.Formation);
        var awayTactics = FormationEffects(away.Formation);

        var homeXg = Math.Clamp(
            1.25 + (homeStrength - awayStrength) / 28.0
            + homeTactics.Attack - awayTactics.Defence * 0.35
            + (homeTactics.Midfield - awayTactics.Midfield) * 0.15,
            0.15, 4.5);

        var awayXg = Math.Clamp(
            1.05 + (awayStrength - homeStrength) / 32.0
            + awayTactics.Attack - homeTactics.Defence * 0.35
            + (awayTactics.Midfield - homeTactics.Midfield) * 0.15,
            0.12, 4.0);

        var hg = Poisson(homeXg);
        var ag = Poisson(awayXg);
        var highlights = options.IncludeHighlights
            ? CreateHighlights(fixture, home, away, hg, ag, options)
            : Array.Empty<MatchHighlight>();

        return new MatchResult
        {
            FixtureId = fixture.Id,
            HomeGoals = hg,
            AwayGoals = ag,
            DurationSeconds = Math.Max(0, options.DurationSeconds),
            Highlights = highlights
        };
    }

    private IReadOnlyList<MatchHighlight> CreateHighlights(
        Fixture fixture, Team home, Team away, int homeGoals, int awayGoals,
        MatchSimulationOptions options)
    {
        var result = new List<MatchHighlight>();
        var count = Math.Max(0, options.HighlightCount);
        var usedMinutes = new HashSet<int>();

        // Goals are always highlights.
        AddGoalHighlights(result, usedMinutes, home, homeGoals, options.MatchMinutes);
        AddGoalHighlights(result, usedMinutes, away, awayGoals, options.MatchMinutes);

        var generic = Math.Max(0, count - result.Count);
        var types = new[] { MatchEventType.Chance, MatchEventType.Save, MatchEventType.Miss, MatchEventType.YellowCard };
        for (var i = 0; i < generic; i++)
        {
            var minute = UniqueMinute(1, Math.Max(1, options.MatchMinutes), usedMinutes);
            var team = _rng.Next(2) == 0 ? home : away;
            var type = types[_rng.Next(types.Length)];
            var text = type switch
            {
                MatchEventType.Chance => $"{team.ShortName} create a good chance.",
                MatchEventType.Save => $"{team.ShortName} goalkeeper makes an important save.",
                MatchEventType.Miss => $"{team.ShortName} go close but miss the target.",
                _ => $"{team.ShortName} are shown a yellow card.",
            };
            result.Add(new MatchHighlight { Minute = minute, TeamId = team.Id, Type = type, Description = text });
        }

        return result.OrderBy(x => x.Minute).ToList();
    }

    private void AddGoalHighlights(List<MatchHighlight> result, HashSet<int> used, Team team, int goals, int matchMinutes)
    {
        for (var i = 0; i < goals; i++)
        {
            var minute = UniqueMinute(1, Math.Max(1, matchMinutes), used);
            result.Add(new MatchHighlight
            {
                Minute = minute,
                TeamId = team.Id,
                Type = MatchEventType.Goal,
                Description = $"{team.ShortName} score! {team.Name} find the net."
            });
        }
    }

    private int UniqueMinute(int min, int max, HashSet<int> used)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var value = _rng.Next(min, max + 1);
            if (used.Add(value)) return value;
        }
        return _rng.Next(min, max + 1);
    }

    private int TeamStrength(Team t)
    {
        if (t.Players.Count == 0) return t.Reputation;
        var available = t.Players.Where(p => !p.Injured && p.SuspensionMatches == 0).ToList();
        return available.Count == 0 ? t.Reputation : (int)available.Average(p => p.Overall);
    }

    private static (double Attack, double Defence, double Midfield) FormationEffects(Formation formation) =>
        formation switch
        {
            Formation.F433 => (0.20, -0.02, 0.04),
            Formation.F4231 => (0.12, 0.10, 0.12),
            Formation.F352 => (0.18, -0.08, 0.18),
            Formation.F343 => (0.28, -0.16, 0.08),
            Formation.F451 => (-0.04, 0.18, 0.20),
            Formation.F4141 => (-0.02, 0.22, 0.12),
            Formation.F532 => (-0.10, 0.34, -0.02),
            Formation.F541 => (-0.18, 0.42, -0.04),
            Formation.F41212 => (0.14, 0.04, 0.18),
            _ => (0.05, 0.05, 0.05) // 4-4-2
        };

    private int Poisson(double lambda)
    {
        var l = Math.Exp(-lambda);
        var k = 0;
        var p = 1.0;
        do { k++; p *= _rng.NextDouble(); } while (p > l);
        return k - 1;
    }
}
