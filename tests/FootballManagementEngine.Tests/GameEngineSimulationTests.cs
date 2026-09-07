using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class GameEngineSimulationTests
{
    private static readonly MatchSimulationOptions Options = new() { DurationSeconds = 3, HighlightCount = 6 };

    /// <summary>A two-team world in a single competition, with one fixture ready to play.</summary>
    private static (FootballGameEngine Engine, Fixture Fixture) World(
        CompetitionType type = CompetitionType.League,
        CompetitionMatchRules? rules = null,
        string? tieId = null)
    {
        var engine = new FootballGameEngine();
        engine.AddTeam(TestData.MakeTeam("HOM"));
        engine.AddTeam(TestData.MakeTeam("AWY"));
        engine.AddCompetition(TestData.MakeCompetition("C", type, rules: rules));

        var fixture = TestData.MakeFixture("C", "HOM", "AWY", tieId: tieId);
        engine.State.Fixtures.Add(fixture);
        return (engine, fixture);
    }

    private static int SeedFor(FootballGameEngine engine, Fixture fixture, Func<MatchResult, bool> predicate) =>
        TestData.FindSeed(
            fixture,
            engine.State.Teams[fixture.HomeTeamId],
            engine.State.Teams[fixture.AwayTeamId],
            Options,
            predicate);

    // ---------- Guard rails ----------

    [Test]
    public void SimulateFixture_ForAnUnknownFixture_Throws()
    {
        var (engine, _) = World();

        Assert.Throws<KeyNotFoundException>(() => engine.SimulateFixture("missing"));
    }

    [Test]
    public void SimulateFixture_ForAnAlreadyPlayedFixture_Throws()
    {
        var (engine, fixture) = World();
        engine.SimulateFixture(fixture.Id, Options, seed: 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => engine.SimulateFixture(fixture.Id, Options, seed: 1));

        Assert.That(exception!.Message, Does.Contain("already has a result"));
    }

    [Test]
    public void SimulateFixture_WithAnUnknownTeam_Throws()
    {
        var (engine, _) = World();
        var orphan = TestData.MakeFixture("C", "GHOST", "AWY");
        engine.State.Fixtures.Add(orphan);

        var exception = Assert.Throws<KeyNotFoundException>(() => engine.SimulateFixture(orphan.Id, Options, 1));

        Assert.That(exception!.Message, Does.Contain("unknown team"));
    }

    [Test]
    public void SimulateFixture_WithAnUnknownCompetition_Throws()
    {
        var (engine, _) = World();
        var orphan = TestData.MakeFixture("NOPE", "HOM", "AWY");
        engine.State.Fixtures.Add(orphan);

        var exception = Assert.Throws<KeyNotFoundException>(() => engine.SimulateFixture(orphan.Id, Options, 1));

        Assert.That(exception!.Message, Does.Contain("NOPE"));
    }

    // ---------- League matches ----------

    [Test]
    public void SimulateFixture_WithASeed_IsReproducible()
    {
        var (first, firstFixture) = World();
        var (second, secondFixture) = World();

        var a = first.SimulateFixture(firstFixture.Id, Options, seed: 4242);
        var b = second.SimulateFixture(secondFixture.Id, Options, seed: 4242);

        Assert.Multiple(() =>
        {
            Assert.That(b.HomeGoals, Is.EqualTo(a.HomeGoals));
            Assert.That(b.AwayGoals, Is.EqualTo(a.AwayGoals));
        });
    }

    [Test]
    public void SimulateFixture_WritesTheResultOntoTheFixture()
    {
        var (engine, fixture) = World();

        var result = engine.SimulateFixture(fixture.Id, Options, seed: 11);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.IsPlayed, Is.True);
            Assert.That(fixture.HomeGoals, Is.EqualTo(result.HomeGoals));
            Assert.That(fixture.AwayGoals, Is.EqualTo(result.AwayGoals));
            Assert.That(result.FixtureId, Is.EqualTo(fixture.Id));
            Assert.That(result.DurationSeconds, Is.EqualTo(3));
        });
    }

    [Test]
    public void SimulateFixture_UpdatesPlayerStats()
    {
        var (engine, fixture) = World();

        engine.SimulateFixture(fixture.Id, Options, seed: 11);

        Assert.That(engine.State.PlayerStats.Values.Count(s => s.Appearances == 1), Is.EqualTo(22));
    }

    [Test]
    public void SimulateFixture_WithoutASeed_StillProducesAResult()
    {
        var (engine, fixture) = World();

        var result = engine.SimulateFixture(fixture.Id, Options);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomeGoals, Is.GreaterThanOrEqualTo(0));
            Assert.That(fixture.IsPlayed, Is.True);
        });
    }

    [Test]
    public void SimulateFixture_ALeagueDraw_StandsWithoutExtraTimeOrPenalties()
    {
        var (engine, fixture) = World();
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomeGoals, Is.EqualTo(result.AwayGoals));
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(engine.State.Fixtures, Has.Count.EqualTo(1), "no replay should be scheduled");
        });
    }

    [Test]
    public void SimulateFixture_ALeagueWin_IsLeftAlone()
    {
        var (engine, fixture) = World();
        var seed = SeedFor(engine, fixture, r => r.HomeGoals != r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(result.AwayPenalties, Is.Null);
        });
    }

    // ---------- Knockout: replays ----------

    [Test]
    public void SimulateFixture_ADrawnReplayableTie_SchedulesAReplay()
    {
        var (engine, fixture) = World(
            CompetitionType.FaCup,
            new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 },
            tieId: "TIE-1");
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);
        var replay = engine.State.Fixtures.Single(f => f.Id != fixture.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReplayRequired, Is.True);
            Assert.That(result.ReplayFixtureId, Is.EqualTo(replay.Id));
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(replay.HomeTeamId, Is.EqualTo("AWY"), "the replay reverses home advantage");
            Assert.That(replay.AwayTeamId, Is.EqualTo("HOM"));
            Assert.That(replay.DateUtc, Is.EqualTo(fixture.DateUtc.AddDays(7)));
            Assert.That(replay.Round, Is.EqualTo(fixture.Round));
            Assert.That(replay.CompetitionId, Is.EqualTo("C"));
            Assert.That(replay.TieId, Is.EqualTo("TIE-1"));
            Assert.That(replay.IsPlayed, Is.False);
        });
    }

    [Test]
    public void SimulateFixture_ADrawnTieWithoutATieId_UsesTheFixtureIdToLinkTheReplay()
    {
        var (engine, fixture) = World(
            CompetitionType.FaCup, new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        engine.SimulateFixture(fixture.Id, Options, seed);
        var replay = engine.State.Fixtures.Single(f => f.Id != fixture.Id);

        Assert.That(replay.TieId, Is.EqualTo(fixture.Id));
    }

    [Test]
    public void SimulateFixture_TheOriginalTieIsStillMarkedPlayed()
    {
        var (engine, fixture) = World(
            CompetitionType.FaCup, new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.IsPlayed, Is.True);
            Assert.That(fixture.HomeGoals, Is.EqualTo(result.HomeGoals));
            Assert.That(fixture.ExtraTimePlayed, Is.False);
        });
    }

    [Test]
    public void SimulateFixture_ADecisiveReplayableTie_SchedulesNoReplay()
    {
        var (engine, fixture) = World(
            CompetitionType.FaCup, new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals != r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(engine.State.Fixtures, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void SimulateFixture_OnceTheReplayLimitIsReached_ResolvesTheTieInstead()
    {
        var rules = new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 1 };
        var (engine, fixture) = World(CompetitionType.FaCup, rules, tieId: "TIE-1");
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        engine.SimulateFixture(fixture.Id, Options, seed);
        var replay = engine.State.Fixtures.Single(f => f.Id != fixture.Id);

        // The replay is drawn too, but the tie has already used its one replay.
        var replaySeed = TestData.FindSeed(
            replay, engine.State.Teams["AWY"], engine.State.Teams["HOM"], Options,
            r => r.HomeGoals == r.AwayGoals);
        var result = engine.SimulateFixture(replay.Id, Options, replaySeed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReplayRequired, Is.False, "a second replay must not be scheduled");
            Assert.That(engine.State.Fixtures, Has.Count.EqualTo(2));
            Assert.That(result.ExtraTime, Is.True);
            Assert.That(HasAWinner(result), Is.True);
        });
    }

    [Test]
    public void SimulateFixture_WithReplaysDisabled_ResolvesADrawImmediately()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup, new CompetitionMatchRules { ReplayAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(engine.State.Fixtures, Has.Count.EqualTo(1));
            Assert.That(HasAWinner(result), Is.True);
        });
    }

    [Test]
    public void SimulateFixture_WithZeroMaxReplays_ResolvesADrawImmediately()
    {
        var (engine, fixture) = World(
            CompetitionType.FaCup, new CompetitionMatchRules { ReplayAllowed = true, MaxReplays = 0 });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(engine.State.Fixtures, Has.Count.EqualTo(1));
        });
    }

    // ---------- Knockout: extra time and penalties ----------

    [Test]
    public void SimulateFixture_ADrawnKnockout_GoesToExtraTimeAndAlwaysFindsAWinner()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup,
            new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = true, PenaltiesAllowed = true });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExtraTime, Is.True);
            Assert.That(fixture.ExtraTimePlayed, Is.True);
            Assert.That(HasAWinner(result), Is.True);
        });
    }

    [Test]
    public void SimulateFixture_ExtraTimeGoalsAreAddedToTheNinetyMinuteScore()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup, new CompetitionMatchRules { ReplayAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);
        var ninetyMinutes = new MatchSimulator(seed)
            .Simulate(fixture, engine.State.Teams["HOM"], engine.State.Teams["AWY"], null, Options);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomeGoals, Is.GreaterThanOrEqualTo(ninetyMinutes.HomeGoals));
            Assert.That(result.AwayGoals, Is.GreaterThanOrEqualTo(ninetyMinutes.AwayGoals));
        });
    }

    [Test]
    public void SimulateFixture_ExtraTimeHighlightsArePushedPastNinetyMinutes()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup, new CompetitionMatchRules { ReplayAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);
        var extraTimeHighlights = result.Highlights.Where(h => h.Description.StartsWith("Extra time:")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(extraTimeHighlights, Is.Not.Empty);
            Assert.That(extraTimeHighlights.Select(h => h.Minute), Is.All.GreaterThan(90));
            Assert.That(result.Highlights.Select(h => h.Minute), Is.Ordered);
        });
    }

    [Test]
    public void SimulateFixture_WithExtraTimeDisabled_GoesStraightToPenalties()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup,
            new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false, PenaltiesAllowed = true });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);
        var ninetyMinutes = new MatchSimulator(seed)
            .Simulate(fixture, engine.State.Teams["HOM"], engine.State.Teams["AWY"], null, Options);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomeGoals, Is.EqualTo(ninetyMinutes.HomeGoals));
            Assert.That(result.AwayGoals, Is.EqualTo(ninetyMinutes.AwayGoals));
            Assert.That(result.HomePenalties, Is.Not.Null);
            Assert.That(result.HomePenalties, Is.Not.EqualTo(result.AwayPenalties));
        });
    }

    [Test]
    public void SimulateFixture_AShootoutIsRecordedAsAHighlight()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup,
            new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false, PenaltiesAllowed = true });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);
        var shootout = result.Highlights.Single(h => h.Type == MatchEventType.PenaltyShootout);

        Assert.Multiple(() =>
        {
            Assert.That(shootout.Minute, Is.EqualTo(120));
            Assert.That(shootout.Description, Does.Contain("on penalties"));
            Assert.That(shootout.Description, Does.Contain($"{result.HomePenalties}"));
        });
    }

    [Test]
    public void SimulateFixture_PenaltiesArePersistedOnTheFixture()
    {
        var (engine, fixture) = World(
            CompetitionType.LeagueCup,
            new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false, PenaltiesAllowed = true });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.HomePenalties, Is.EqualTo(result.HomePenalties));
            Assert.That(fixture.AwayPenalties, Is.EqualTo(result.AwayPenalties));
        });
    }

    [Test]
    public void SimulateFixture_WithNeitherExtraTimeNorPenalties_LeavesTheTieDrawn()
    {
        var (engine, fixture) = World(
            CompetitionType.EflTrophy,
            new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false, PenaltiesAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.Multiple(() =>
        {
            Assert.That(result.HomeGoals, Is.EqualTo(result.AwayGoals));
            Assert.That(result.ExtraTime, Is.False);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(result.AwayPenalties, Is.Null);
        });
    }

    [Test]
    public void SimulateFixture_ExtraTimeThatSettlesTheTie_NeedsNoShootout()
    {
        // Extra time uses its own clock-based seed, so run the same drawn tie repeatedly and
        // assert the invariant: penalties appear only when extra time left the scores level.
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var (engine, fixture) = World(
                CompetitionType.LeagueCup, new CompetitionMatchRules { ReplayAllowed = false });
            var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

            var result = engine.SimulateFixture(fixture.Id, Options, seed);

            if (result.HomeGoals == result.AwayGoals)
                Assert.That(result.HomePenalties, Is.Not.Null, "a level tie must go to penalties");
            else
                Assert.That(result.HomePenalties, Is.Null, "a settled tie must not go to penalties");
        }
    }

    [Test]
    public void SimulateFixture_AShootoutAlwaysSeparatesTheTeams()
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var (engine, fixture) = World(
                CompetitionType.LeagueCup,
                new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false });
            var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

            var result = engine.SimulateFixture(fixture.Id, Options, seed);

            Assert.Multiple(() =>
            {
                Assert.That(result.HomePenalties, Is.Not.EqualTo(result.AwayPenalties));
                Assert.That(result.HomePenalties, Is.GreaterThanOrEqualTo(0));
                Assert.That(result.AwayPenalties, Is.GreaterThanOrEqualTo(0));
            });
        }
    }

    [Test]
    public void SimulateFixture_AShootoutWorksForSquadsWithoutGoalkeepersOrForwards()
    {
        var engine = new FootballGameEngine();
        engine.AddTeam(TestData.MakeTeam("HOM", squadSize: 0));
        engine.AddTeam(TestData.MakeTeam("AWY", squadSize: 0));
        engine.AddCompetition(TestData.MakeCompetition(
            "C", CompetitionType.LeagueCup,
            rules: new CompetitionMatchRules { ReplayAllowed = false, ExtraTimeAllowed = false }));
        var fixture = TestData.MakeFixture("C", "HOM", "AWY");
        engine.State.Fixtures.Add(fixture);

        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);
        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.That(HasAWinner(result), Is.True);
    }

    [Test]
    public void SimulateFixture_EuropeanKnockoutDrawsAreResolvedLikeAnyOtherCup()
    {
        var (engine, fixture) = World(
            CompetitionType.EuropeanKnockout, new CompetitionMatchRules { ReplayAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.That(HasAWinner(result), Is.True);
    }

    [Test]
    public void SimulateFixture_EuropeanLeaguePhaseDrawsAreAlsoResolved_DocumentsCurrentBehaviour()
    {
        // The league-phase group stage is not CompetitionType.League, so the engine treats a
        // drawn game as a knockout tie and forces a winner. Update this test if draws are
        // ever allowed to stand in the league phase.
        var (engine, fixture) = World(
            CompetitionType.EuropeanLeaguePhase, new CompetitionMatchRules { ReplayAllowed = false });
        var seed = SeedFor(engine, fixture, r => r.HomeGoals == r.AwayGoals);

        var result = engine.SimulateFixture(fixture.Id, Options, seed);

        Assert.That(result.ExtraTime, Is.True);
    }

    [Test]
    public void SimulateFixture_HonoursCustomSimulationOptions()
    {
        var (engine, fixture) = World();

        var result = engine.SimulateFixture(
            fixture.Id,
            new MatchSimulationOptions { DurationSeconds = 20, IncludeHighlights = false, MatchMinutes = 45 },
            seed: 8);

        Assert.Multiple(() =>
        {
            Assert.That(result.DurationSeconds, Is.EqualTo(20));
            Assert.That(result.Highlights, Is.Empty);
        });
    }

    [Test]
    public void SimulateFixture_WithoutOptions_UsesTheDefaults()
    {
        var (engine, fixture) = World();

        var result = engine.SimulateFixture(fixture.Id, seed: 8);

        Assert.That(result.DurationSeconds, Is.EqualTo(8));
    }

    private static bool HasAWinner(MatchResult result) =>
        result.HomeGoals != result.AwayGoals ||
        (result.HomePenalties.HasValue && result.HomePenalties != result.AwayPenalties);
}
