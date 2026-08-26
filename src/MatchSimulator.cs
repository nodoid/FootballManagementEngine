namespace FootballManagementEngine;

public sealed class MatchSimulator
{
    private readonly Random _rng;

    public MatchSimulator(int seed = 12345) => _rng = new Random(seed);

    public MatchResult Simulate(Fixture fixture, Team home, Team away)
    {
        var homeStrength = TeamStrength(home);
        var awayStrength = TeamStrength(away);

        var homeXg = Math.Clamp(1.25 + (homeStrength - awayStrength) / 28.0, 0.2, 4.0);
        var awayXg = Math.Clamp(1.05 + (awayStrength - homeStrength) / 32.0, 0.2, 3.5);

        var hg = Poisson(homeXg);
        var ag = Poisson(awayXg);

        return new MatchResult
        {
            FixtureId = fixture.Id,
            HomeGoals = hg,
            AwayGoals = ag
        };
    }

    private int TeamStrength(Team t)
    {
        if (t.Players.Count == 0) return t.Reputation;
        return (int)t.Players.Average(p => p.Overall);
    }

    private int Poisson(double lambda)
    {
        var l = Math.Exp(-lambda);
        var k = 0;
        var p = 1.0;
        do { k++; p *= _rng.NextDouble(); } while (p > l);
        return k - 1;
    }
}
