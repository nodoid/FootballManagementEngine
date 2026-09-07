using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class MatchSimulatorTests
{
    private Fixture _fixture = null!;
    private Team _home = null!;
    private Team _away = null!;

    [SetUp]
    public void SetUp()
    {
        _home = TestData.MakeTeam("HOM");
        _away = TestData.MakeTeam("AWY");
        _fixture = TestData.MakeFixture("PL-COMP", "HOM", "AWY");
    }

    /// <summary>Aggregates goals over a fixed, deterministic band of seeds.</summary>
    private static (double Home, double Away) AverageGoals(
        Fixture fixture, Team home, Team away, int samples = 400)
    {
        var options = new MatchSimulationOptions { IncludeHighlights = false };
        var h = 0; var a = 0;
        for (var seed = 1; seed <= samples; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(fixture, home, away, null, options);
            h += result.HomeGoals;
            a += result.AwayGoals;
        }
        return (h / (double)samples, a / (double)samples);
    }

    [Test]
    public void Options_HaveSensibleDefaults()
    {
        var options = new MatchSimulationOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.DurationSeconds, Is.EqualTo(8));
            Assert.That(options.IncludeHighlights, Is.True);
            Assert.That(options.HighlightCount, Is.EqualTo(10));
            Assert.That(options.MatchMinutes, Is.EqualTo(90));
        });
    }

    [Test]
    public void Simulate_IsDeterministicForAGivenSeed()
    {
        var first = new MatchSimulator(777).Simulate(_fixture, _home, _away);
        var second = new MatchSimulator(777).Simulate(_fixture, _home, _away);

        Assert.Multiple(() =>
        {
            Assert.That(second.HomeGoals, Is.EqualTo(first.HomeGoals));
            Assert.That(second.AwayGoals, Is.EqualTo(first.AwayGoals));
            Assert.That(
                second.Highlights.Select(h => (h.Minute, h.TeamId, h.Type, h.Description)),
                Is.EqualTo(first.Highlights.Select(h => (h.Minute, h.TeamId, h.Type, h.Description))));
        });
    }

    [Test]
    public void Simulate_DifferentSeeds_ProduceDifferentMatches()
    {
        var results = Enumerable.Range(1, 40)
            .Select(seed => new MatchSimulator(seed).Simulate(_fixture, _home, _away))
            .Select(r => (r.HomeGoals, r.AwayGoals))
            .Distinct()
            .ToList();

        Assert.That(results, Has.Count.GreaterThan(1));
    }

    [Test]
    public void Simulate_CarriesTheFixtureIdIntoTheResult()
    {
        var result = new MatchSimulator(1).Simulate(_fixture, _home, _away);

        Assert.That(result.FixtureId, Is.EqualTo(_fixture.Id));
    }

    [Test]
    public void Simulate_NeverReturnsNegativeGoals()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away);
            Assert.That(result.HomeGoals, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
            Assert.That(result.AwayGoals, Is.GreaterThanOrEqualTo(0), $"seed {seed}");
        }
    }

    [Test]
    public void Simulate_LeavesKnockoutFieldsUnsetForTheCaller()
    {
        var result = new MatchSimulator(1).Simulate(_fixture, _home, _away);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(result.AwayPenalties, Is.Null);
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(result.DateUtc, Is.Null);
        });
    }

    [Test]
    public void Simulate_EchoesTheRequestedDuration()
    {
        var result = new MatchSimulator(1).Simulate(
            _fixture, _home, _away, options: new MatchSimulationOptions { DurationSeconds = 30 });

        Assert.That(result.DurationSeconds, Is.EqualTo(30));
    }

    [Test]
    public void Simulate_ClampsANegativeDurationToZero()
    {
        var result = new MatchSimulator(1).Simulate(
            _fixture, _home, _away, options: new MatchSimulationOptions { DurationSeconds = -5 });

        Assert.That(result.DurationSeconds, Is.Zero);
    }

    [Test]
    public void Simulate_WithoutOptions_UsesTheDefaults()
    {
        var result = new MatchSimulator(3).Simulate(_fixture, _home, _away);

        Assert.Multiple(() =>
        {
            Assert.That(result.DurationSeconds, Is.EqualTo(8));
            Assert.That(result.Highlights, Is.Not.Empty);
        });
    }

    // ---------- Highlights ----------

    [Test]
    public void Simulate_WithHighlightsDisabled_ReturnsNone()
    {
        var result = new MatchSimulator(5).Simulate(
            _fixture, _home, _away, options: new MatchSimulationOptions { IncludeHighlights = false });

        Assert.That(result.Highlights, Is.Empty);
    }

    [Test]
    public void Simulate_ProducesOneGoalHighlightPerGoal()
    {
        for (var seed = 1; seed <= 60; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away);
            var goals = result.Highlights.Where(h => h.Type == MatchEventType.Goal).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(goals.Count(h => h.TeamId == _home.Id), Is.EqualTo(result.HomeGoals), $"seed {seed} home");
                Assert.That(goals.Count(h => h.TeamId == _away.Id), Is.EqualTo(result.AwayGoals), $"seed {seed} away");
            });
        }
    }

    [Test]
    public void Simulate_PadsUpToTheRequestedHighlightCount()
    {
        var options = new MatchSimulationOptions { HighlightCount = 12 };

        for (var seed = 1; seed <= 40; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away, options: options);
            var totalGoals = result.HomeGoals + result.AwayGoals;

            Assert.That(result.Highlights, Has.Count.EqualTo(Math.Max(12, totalGoals)), $"seed {seed}");
        }
    }

    [Test]
    public void Simulate_WithZeroHighlightCount_StillReportsTheGoals()
    {
        var options = new MatchSimulationOptions { HighlightCount = 0, InjuryChance = 0 };
        var seed = TestData.FindSeed(_fixture, _home, _away, options, r => r.HomeGoals + r.AwayGoals > 0);

        var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away, options: options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Highlights, Has.Count.EqualTo(result.HomeGoals + result.AwayGoals));
            Assert.That(result.Highlights.Select(h => h.Type), Is.All.EqualTo(MatchEventType.Goal));
        });
    }

    [Test]
    public void Simulate_WithANegativeHighlightCount_TreatsItAsZero()
    {
        var options = new MatchSimulationOptions { HighlightCount = -4 };

        var result = new MatchSimulator(9).Simulate(_fixture, _home, _away, options: options);

        Assert.That(result.Highlights, Has.Count.EqualTo(result.HomeGoals + result.AwayGoals));
    }

    [Test]
    public void Simulate_OrdersHighlightsByMinute()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away);
            Assert.That(result.Highlights.Select(h => h.Minute), Is.Ordered, $"seed {seed}");
        }
    }

    [Test]
    public void Simulate_KeepsHighlightMinutesInsideTheMatch()
    {
        var options = new MatchSimulationOptions { MatchMinutes = 30, HighlightCount = 6 };

        for (var seed = 1; seed <= 40; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(_fixture, _home, _away, options: options);
            Assert.That(result.Highlights.Select(h => h.Minute), Is.All.InRange(1, 30), $"seed {seed}");
        }
    }

    [Test]
    public void Simulate_AttributesEveryHighlightToOneOfThePlayingTeams()
    {
        var result = new MatchSimulator(21).Simulate(_fixture, _home, _away);

        Assert.That(result.Highlights.Select(h => h.TeamId), Is.All.AnyOf("HOM", "AWY"));
    }

    [Test]
    public void Simulate_OnlyUsesTheSupportedHighlightTypes()
    {
        var seen = Enumerable.Range(1, 120)
            .SelectMany(seed => new MatchSimulator(seed).Simulate(_fixture, _home, _away).Highlights)
            .Select(h => h.Type)
            .Distinct()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(seen, Is.SubsetOf(new[]
            {
                MatchEventType.Goal, MatchEventType.Chance, MatchEventType.Save,
                MatchEventType.Miss, MatchEventType.YellowCard, MatchEventType.Injury
            }));
            Assert.That(seen, Does.Contain(MatchEventType.Goal));
            Assert.That(seen, Does.Contain(MatchEventType.YellowCard));
        });
    }

    [Test]
    public void Simulate_DescribesGoalsWithTheScoringClub()
    {
        var seed = TestData.FindSeed(_fixture, _home, _away, new MatchSimulationOptions(), r => r.HomeGoals > 0);

        var goal = new MatchSimulator(seed).Simulate(_fixture, _home, _away)
            .Highlights.First(h => h.Type == MatchEventType.Goal && h.TeamId == "HOM");

        Assert.Multiple(() =>
        {
            Assert.That(goal.Description, Does.Contain("HOM"));
            Assert.That(goal.Description, Does.Contain(_home.Name));
        });
    }

    [Test]
    public void Simulate_GivesEveryHighlightADescription()
    {
        var result = new MatchSimulator(33).Simulate(_fixture, _home, _away);

        Assert.That(result.Highlights.Select(h => h.Description), Is.All.Not.Empty);
    }

    // ---------- Strength and tactics ----------

    [Test]
    public void Simulate_TheStrongerSquadScoresMoreOverManyMatches()
    {
        var strong = TestData.MakeTeam("STR", overall: 90);
        var weak = TestData.MakeTeam("WEA", overall: 50);
        var fixture = TestData.MakeFixture("PL-COMP", "STR", "WEA");

        var (homeAvg, awayAvg) = AverageGoals(fixture, strong, weak);

        Assert.That(homeAvg, Is.GreaterThan(awayAvg + 0.5));
    }

    [Test]
    public void Simulate_HomeAdvantage_ShowsUpBetweenEvenlyMatchedSides()
    {
        var (homeAvg, awayAvg) = AverageGoals(_fixture, _home, _away);

        Assert.That(homeAvg, Is.GreaterThan(awayAvg));
    }

    [Test]
    public void Simulate_AnAttackingFormationOutscoresADefensiveOne()
    {
        var attacking = TestData.MakeTeam("ATT", formation: Formation.F343);
        var defensive = TestData.MakeTeam("DEF", formation: Formation.F541);
        var opponent = TestData.MakeTeam("OPP");

        var attackingAvg = AverageGoals(TestData.MakeFixture("C", "ATT", "OPP"), attacking, opponent).Home;
        var defensiveAvg = AverageGoals(TestData.MakeFixture("C", "DEF", "OPP"), defensive, opponent).Home;

        Assert.That(attackingAvg, Is.GreaterThan(defensiveAvg));
    }

    [Test]
    public void Simulate_ADefensiveFormationConcedesLess()
    {
        var defensive = TestData.MakeTeam("DEF", formation: Formation.F541);
        var attacking = TestData.MakeTeam("ATT", formation: Formation.F343);
        var opponent = TestData.MakeTeam("OPP");

        var concededByDefensive = AverageGoals(TestData.MakeFixture("C", "DEF", "OPP"), defensive, opponent).Away;
        var concededByAttacking = AverageGoals(TestData.MakeFixture("C", "ATT", "OPP"), attacking, opponent).Away;

        Assert.That(concededByDefensive, Is.LessThan(concededByAttacking));
    }

    [Test]
    [TestCaseSource(nameof(AllFormations))]
    public void Simulate_SupportsEveryFormation(Formation formation)
    {
        var home = TestData.MakeTeam("HOM", formation: formation);

        var result = new MatchSimulator(4).Simulate(TestData.MakeFixture("C", "HOM", "AWY"), home, _away);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomeGoals, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.AwayGoals, Is.GreaterThanOrEqualTo(0));
        });
    }

    public static IEnumerable<Formation> AllFormations() => Enum.GetValues<Formation>();

    [Test]
    public void Simulate_WithAnEmptySquad_FallsBackToReputation()
    {
        var reputable = TestData.MakeTeam("REP", squadSize: 0, reputation: 95);
        var poor = TestData.MakeTeam("POO", squadSize: 0, reputation: 40);
        var fixture = TestData.MakeFixture("C", "REP", "POO");

        var (homeAvg, awayAvg) = AverageGoals(fixture, reputable, poor);

        Assert.That(homeAvg, Is.GreaterThan(awayAvg + 0.5));
    }

    [Test]
    public void Simulate_IgnoresInjuredAndSuspendedPlayersWhenRatingASquad()
    {
        // A squad of stars where every star is unavailable falls back to reputation,
        // so it plays exactly like an empty squad with the same reputation.
        var depleted = TestData.MakeTeam("DEP", overall: 95, reputation: 40);
        foreach (var player in depleted.Players) player.Injured = true;
        var empty = TestData.MakeTeam("DEP", squadSize: 0, reputation: 40);

        var depletedGoals = AverageGoals(TestData.MakeFixture("C", "DEP", "AWY"), depleted, _away, 100);
        var emptyGoals = AverageGoals(TestData.MakeFixture("C", "DEP", "AWY"), empty, _away, 100);

        Assert.That(depletedGoals.Home, Is.EqualTo(emptyGoals.Home));
    }

    [Test]
    public void Simulate_SuspendedPlayersAreAlsoExcluded()
    {
        var team = TestData.MakeTeam("SUS", overall: 95, reputation: 40);
        foreach (var player in team.Players) player.SuspensionMatches = 1;
        var empty = TestData.MakeTeam("SUS", squadSize: 0, reputation: 40);

        var suspendedGoals = AverageGoals(TestData.MakeFixture("C", "SUS", "AWY"), team, _away, 100);
        var emptyGoals = AverageGoals(TestData.MakeFixture("C", "SUS", "AWY"), empty, _away, 100);

        Assert.That(suspendedGoals.Home, Is.EqualTo(emptyGoals.Home));
    }

    [Test]
    public void Simulate_OnlyAvailablePlayersRaiseTheSquadRating()
    {
        var mixed = TestData.MakeTeam("MIX", overall: 50);
        foreach (var player in mixed.Players.Take(5)) player.Overall = 95;
        var raised = TestData.MakeTeam("MIX", overall: 50);
        foreach (var player in raised.Players.Take(5)) player.Overall = 95;
        foreach (var player in raised.Players.Take(5)) player.Injured = true;

        var withStars = AverageGoals(TestData.MakeFixture("C", "MIX", "AWY"), mixed, _away, 200).Home;
        var withoutStars = AverageGoals(TestData.MakeFixture("C", "MIX", "AWY"), raised, _away, 200).Home;

        Assert.That(withStars, Is.GreaterThan(withoutStars));
    }

    [Test]
    public void Simulate_ExtremeMismatch_StaysWithinTheClampedGoalRange()
    {
        var strong = TestData.MakeTeam("STR", overall: 99, formation: Formation.F343, reputation: 99);
        var weak = TestData.MakeTeam("WEA", overall: 1, formation: Formation.F343, reputation: 1);
        var fixture = TestData.MakeFixture("C", "STR", "WEA");

        var (homeAvg, awayAvg) = AverageGoals(fixture, strong, weak);

        Assert.Multiple(() =>
        {
            // xG is clamped to [0.15, 4.5] home and [0.12, 4.0] away.
            Assert.That(homeAvg, Is.LessThanOrEqualTo(5.5));
            Assert.That(awayAvg, Is.GreaterThanOrEqualTo(0));
            Assert.That(awayAvg, Is.LessThan(homeAvg));
        });
    }

    [Test]
    public void Simulate_TheSameSimulatorAdvancesItsSequenceBetweenMatches()
    {
        var simulator = new MatchSimulator(555);

        var results = Enumerable.Range(0, 20)
            .Select(_ => simulator.Simulate(_fixture, _home, _away))
            .Select(r => (r.HomeGoals, r.AwayGoals))
            .Distinct()
            .ToList();

        Assert.That(results, Has.Count.GreaterThan(1), "a reused simulator must not replay the same match");
    }
}
