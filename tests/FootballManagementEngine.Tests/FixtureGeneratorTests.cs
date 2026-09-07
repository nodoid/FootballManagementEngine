using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class FixtureGeneratorTests
{
    private static readonly DateTime Wednesday = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static List<Team> Teams(int count) =>
        Enumerable.Range(1, count).Select(i => TestData.MakeTeam($"T{i}")).ToList();

    // ---------- DoubleRoundRobin ----------

    [TestCase(0)]
    [TestCase(1)]
    public void DoubleRoundRobin_WithFewerThanTwoTeams_ReturnsNoFixtures(int teamCount)
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(teamCount), Wednesday);

        Assert.That(fixtures, Is.Empty);
    }

    [TestCase(2, 2)]
    [TestCase(4, 12)]
    [TestCase(6, 30)]
    [TestCase(20, 380)]
    public void DoubleRoundRobin_WithAnEvenSquadCount_PlaysEveryTeamTwice(int teamCount, int expected)
    {
        var teams = Teams(teamCount);

        var fixtures = FixtureGenerator.DoubleRoundRobin(TestData.MakeCompetition("C"), teams, Wednesday);

        Assert.That(fixtures, Has.Count.EqualTo(expected));
        foreach (var team in teams)
        {
            var played = fixtures.Count(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id);
            Assert.That(played, Is.EqualTo(2 * (teamCount - 1)), $"{team.Id} plays the wrong number of games");
        }
    }

    [TestCase(4)]
    [TestCase(6)]
    [TestCase(8)]
    public void DoubleRoundRobin_PairsEveryTeamOnceHomeAndOnceAway(int teamCount)
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(teamCount), Wednesday);

        var orderedPairs = fixtures.Select(f => (f.HomeTeamId, f.AwayTeamId)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(orderedPairs.Distinct().Count(), Is.EqualTo(orderedPairs.Count),
                "the same ordered pairing was scheduled twice");
            Assert.That(orderedPairs, Has.Count.EqualTo(teamCount * (teamCount - 1)));
            Assert.That(fixtures.Any(f => f.HomeTeamId == f.AwayTeamId), Is.False);
        });
    }

    [Test]
    public void DoubleRoundRobin_WithAnOddSquadCount_GivesEachRoundABye()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(5), Wednesday);

        Assert.Multiple(() =>
        {
            // 5 teams padded to 6: 3 rounds x 2 real matches, doubled = 20.
            Assert.That(fixtures, Has.Count.EqualTo(20));
            Assert.That(fixtures.Any(f => f.HomeTeamId == "BYE" || f.AwayTeamId == "BYE"), Is.False,
                "the placeholder BYE team leaked into the schedule");
        });
    }

    [Test]
    public void DoubleRoundRobin_TagsEveryFixtureWithTheCompetition()
    {
        var competition = TestData.MakeCompetition("PL-COMP");

        var fixtures = FixtureGenerator.DoubleRoundRobin(competition, Teams(4), Wednesday);

        Assert.That(fixtures.Select(f => f.CompetitionId), Is.All.EqualTo("PL-COMP"));
    }

    [Test]
    public void DoubleRoundRobin_NumbersRoundsContiguouslyAcrossBothHalves()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(6), Wednesday);

        var rounds = fixtures.Select(f => f.Round).Distinct().OrderBy(r => r).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rounds, Is.EqualTo(Enumerable.Range(1, 10).ToList()));
            foreach (var round in rounds)
                Assert.That(fixtures.Count(f => f.Round == round), Is.EqualTo(3), $"round {round}");
        });
    }

    [Test]
    public void DoubleRoundRobin_StartsOnTheFirstMatchingWeekdayAtThreeOClock()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), Wednesday);

        var openingDay = fixtures.Where(f => f.Round == 1).Select(f => f.DateUtc).Distinct().Single();

        Assert.Multiple(() =>
        {
            Assert.That(openingDay.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday));
            Assert.That(openingDay.TimeOfDay, Is.EqualTo(TimeSpan.FromHours(15)));
            Assert.That(openingDay.Date, Is.GreaterThanOrEqualTo(Wednesday.Date));
            Assert.That(openingDay.Date - Wednesday.Date, Is.LessThan(TimeSpan.FromDays(7)));
        });
    }

    [Test]
    public void DoubleRoundRobin_StartingOnTheTargetDay_DoesNotSkipAWeek()
    {
        var saturday = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        Assume.That(saturday.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday));

        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), saturday);

        Assert.That(fixtures.First(f => f.Round == 1).DateUtc, Is.EqualTo(saturday.Date.AddHours(15)));
    }

    [Test]
    public void DoubleRoundRobin_HonoursACustomMatchDay()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), Wednesday, DayOfWeek.Sunday);

        Assert.That(
            fixtures.Where(f => f.Round <= 3).Select(f => f.DateUtc.DayOfWeek),
            Is.All.EqualTo(DayOfWeek.Sunday));
    }

    [Test]
    public void DoubleRoundRobin_ReverseFixtures_DriftOffTheMatchDay()
    {
        // Documents current behaviour: the reverse half is scheduled with AddMonths(5), which
        // moves it off the configured match day (1 Aug 2026 Saturday -> 1 Jan 2027 Friday).
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), Wednesday);

        var reverseDays = fixtures.Where(f => f.Round > 3).Select(f => f.DateUtc.DayOfWeek).Distinct().ToList();

        Assert.That(reverseDays, Does.Not.Contain(DayOfWeek.Saturday),
            "if the generator is fixed to re-align the reverse half, update this test");
    }

    [Test]
    public void DoubleRoundRobin_SpacesFirstHalfRoundsAWeekApart()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(6), Wednesday);

        var firstHalf = Enumerable.Range(1, 5)
            .Select(round => fixtures.Where(f => f.Round == round).Select(f => f.DateUtc).Distinct().Single())
            .ToList();

        for (var i = 1; i < firstHalf.Count; i++)
            Assert.That(firstHalf[i] - firstHalf[i - 1], Is.EqualTo(TimeSpan.FromDays(7)), $"gap before round {i + 1}");
    }

    [Test]
    public void DoubleRoundRobin_SchedulesTheReverseFixtureFiveMonthsLater()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), Wednesday);

        foreach (var first in fixtures.Where(f => f.Round <= 3))
        {
            var reverse = fixtures.Single(f =>
                f.HomeTeamId == first.AwayTeamId && f.AwayTeamId == first.HomeTeamId);

            Assert.Multiple(() =>
            {
                Assert.That(reverse.Round, Is.EqualTo(first.Round + 3));
                Assert.That(reverse.DateUtc, Is.EqualTo(first.DateUtc.AddMonths(5)));
            });
        }
    }

    [Test]
    public void DoubleRoundRobin_ProducesUnplayedFixtures()
    {
        var fixtures = FixtureGenerator.DoubleRoundRobin(
            TestData.MakeCompetition("C"), Teams(4), Wednesday);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Select(f => f.IsPlayed), Is.All.False);
            Assert.That(fixtures.Select(f => f.HomeGoals), Is.All.Null);
            Assert.That(fixtures.Select(f => f.Id).Distinct().Count(), Is.EqualTo(fixtures.Count));
        });
    }

    // ---------- CupRound ----------

    [TestCase(8, 4)]
    [TestCase(2, 1)]
    [TestCase(1, 0)]
    [TestCase(0, 0)]
    public void CupRound_PairsTeamsTwoAtATime(int teamCount, int expected)
    {
        var fixtures = FixtureGenerator.CupRound(
            TestData.MakeCompetition("FA", CompetitionType.FaCup),
            Teams(teamCount), 1, Wednesday, new Random(1));

        Assert.That(fixtures, Has.Count.EqualTo(expected));
    }

    [Test]
    public void CupRound_WithAnOddEntry_LeavesExactlyOneTeamWithoutATie()
    {
        var teams = Teams(7);

        var fixtures = FixtureGenerator.CupRound(
            TestData.MakeCompetition("FA", CompetitionType.FaCup), teams, 1, Wednesday, new Random(3));

        var drawn = fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(3));
            Assert.That(drawn, Has.Count.EqualTo(6));
            Assert.That(drawn.Distinct().Count(), Is.EqualTo(6), "a team was drawn twice");
            Assert.That(teams.Count(t => !drawn.Contains(t.Id)), Is.EqualTo(1));
        });
    }

    [Test]
    public void CupRound_NeverDrawsATeamAgainstItself()
    {
        var fixtures = FixtureGenerator.CupRound(
            TestData.MakeCompetition("FA", CompetitionType.FaCup), Teams(16), 2, Wednesday, new Random(99));

        Assert.That(fixtures.Any(f => f.HomeTeamId == f.AwayTeamId), Is.False);
    }

    [Test]
    public void CupRound_StampsRoundDateAndCompetitionOnEveryTie()
    {
        var competition = TestData.MakeCompetition("FA", CompetitionType.FaCup);

        var fixtures = FixtureGenerator.CupRound(competition, Teams(8), 3, Wednesday, new Random(7));

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Select(f => f.CompetitionId), Is.All.EqualTo("FA"));
            Assert.That(fixtures.Select(f => f.Round), Is.All.EqualTo(3));
            Assert.That(fixtures.Select(f => f.DateUtc), Is.All.EqualTo(Wednesday));
        });
    }

    [Test]
    public void CupRound_GivesEachTieItsOwnIdentifier()
    {
        var fixtures = FixtureGenerator.CupRound(
            TestData.MakeCompetition("FA", CompetitionType.FaCup), Teams(8), 4, Wednesday, new Random(11));

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Select(f => f.TieId), Is.All.Not.Null);
            Assert.That(fixtures.Select(f => f.TieId).Distinct().Count(), Is.EqualTo(fixtures.Count));
            Assert.That(fixtures.Select(f => f.TieId), Is.All.StartWith("FA-4-"));
        });
    }

    [Test]
    public void CupRound_IsReproducibleForAGivenSeed()
    {
        var teams = Teams(8);
        var competition = TestData.MakeCompetition("FA", CompetitionType.FaCup);

        var first = FixtureGenerator.CupRound(competition, teams, 1, Wednesday, new Random(42));
        var second = FixtureGenerator.CupRound(competition, teams, 1, Wednesday, new Random(42));

        Assert.That(
            second.Select(f => (f.HomeTeamId, f.AwayTeamId)),
            Is.EqualTo(first.Select(f => (f.HomeTeamId, f.AwayTeamId))));
    }

    [Test]
    public void CupRound_ShufflesTheDraw()
    {
        var teams = Teams(16);
        var competition = TestData.MakeCompetition("FA", CompetitionType.FaCup);

        var seeded = FixtureGenerator.CupRound(competition, teams, 1, Wednesday, new Random(5))
            .Select(f => (f.HomeTeamId, f.AwayTeamId)).ToList();
        var inOrder = Enumerable.Range(0, 8)
            .Select(i => ($"T{i * 2 + 1}", $"T{i * 2 + 2}")).ToList();

        Assert.That(seeded, Is.Not.EqualTo(inOrder));
    }

    // ---------- EuropeanLeaguePhase ----------

    [TestCase(0)]
    [TestCase(1)]
    public void EuropeanLeaguePhase_WithFewerThanTwoTeams_ReturnsNoFixtures(int teamCount)
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), Teams(teamCount), Wednesday);

        Assert.That(fixtures, Is.Empty);
    }

    [Test]
    public void EuropeanLeaguePhase_GivesEveryTeamOneGamePerRound()
    {
        var teams = Teams(6);

        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), teams, Wednesday);

        Assert.That(fixtures, Has.Count.EqualTo(8 * 3));
        foreach (var round in Enumerable.Range(1, 8))
        {
            var inRound = fixtures.Where(f => f.Round == round).ToList();
            var appearances = inRound.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(inRound, Has.Count.EqualTo(3), $"round {round}");
                Assert.That(appearances.Distinct().Count(), Is.EqualTo(6), $"round {round} repeats a team");
            });
        }
    }

    [Test]
    public void EuropeanLeaguePhase_HonoursACustomRoundCount()
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), Teams(4), Wednesday, rounds: 3);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(3 * 2));
            Assert.That(fixtures.Select(f => f.Round).Distinct().OrderBy(r => r), Is.EqualTo(new[] { 1, 2, 3 }));
        });
    }

    [Test]
    public void EuropeanLeaguePhase_PlaysARoundEverySevenDaysFromTheGivenStart()
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), Teams(6), Wednesday);

        foreach (var round in Enumerable.Range(1, 8))
        {
            var date = fixtures.Where(f => f.Round == round).Select(f => f.DateUtc).Distinct().Single();
            Assert.That(date, Is.EqualTo(Wednesday.AddDays((round - 1) * 7)), $"round {round}");
        }
    }

    [Test]
    public void EuropeanLeaguePhase_WithAnOddEntry_DropsTheByeAndShortensThoseRounds()
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), Teams(5), Wednesday);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Any(f => f.HomeTeamId == "BYE" || f.AwayTeamId == "BYE"), Is.False);
            Assert.That(fixtures, Has.Count.EqualTo(8 * 2));
            Assert.That(fixtures.Any(f => f.HomeTeamId == f.AwayTeamId), Is.False);
        });
    }

    [Test]
    public void EuropeanLeaguePhase_TagsEveryFixtureWithTheCompetition()
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UEL", CompetitionType.EuropeanLeaguePhase), Teams(4), Wednesday);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures.Select(f => f.CompetitionId), Is.All.EqualTo("UEL"));
            Assert.That(fixtures.Select(f => f.IsPlayed), Is.All.False);
        });
    }

    [Test]
    public void EuropeanLeaguePhase_WithExactlyTwoTeams_SchedulesOneMatchPerRound()
    {
        var fixtures = FixtureGenerator.EuropeanLeaguePhase(
            TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase), Teams(2), Wednesday, rounds: 4);

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(4));
            Assert.That(fixtures.Count(f => f.HomeTeamId == "T1"), Is.EqualTo(2), "home advantage should alternate");
            Assert.That(fixtures.Count(f => f.HomeTeamId == "T2"), Is.EqualTo(2));
        });
    }
}
