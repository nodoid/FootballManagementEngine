using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class LeagueTableTests
{
    private Dictionary<string, Team> _teams = null!;
    private League _league = null!;

    [SetUp]
    public void SetUp()
    {
        _teams = new[] { "A", "B", "C", "D" }
            .ToDictionary(id => id, id => TestData.MakeTeam(id, name: $"{id} United"));
        _league = TestData.MakeLeague("PL", 1, _teams.Keys);
    }

    private static Fixture Played(string home, string away, int homeGoals, int awayGoals) => new()
    {
        CompetitionId = "PL-COMP",
        HomeTeamId = home,
        AwayTeamId = away,
        IsPlayed = true,
        HomeGoals = homeGoals,
        AwayGoals = awayGoals
    };

    [Test]
    public void Build_WithNoFixtures_ListsEveryTeamOnZero()
    {
        var table = LeagueTable.Build(_league, _teams, []);

        Assert.Multiple(() =>
        {
            Assert.That(table, Has.Count.EqualTo(4));
            Assert.That(table.Select(r => r.Played), Is.All.Zero);
            Assert.That(table.Select(r => r.Points), Is.All.Zero);
            Assert.That(table.Select(r => r.TeamId), Is.EquivalentTo(new[] { "A", "B", "C", "D" }));
        });
    }

    [Test]
    public void Build_TakesTeamNamesFromTheTeamDictionary()
    {
        var table = LeagueTable.Build(_league, _teams, []);

        Assert.That(table.Single(r => r.TeamId == "A").TeamName, Is.EqualTo("A United"));
    }

    [Test]
    public void Build_AwardsThreePointsForAHomeWin()
    {
        var table = LeagueTable.Build(_league, _teams, [Played("A", "B", 2, 0)]);

        var a = table.Single(r => r.TeamId == "A");
        var b = table.Single(r => r.TeamId == "B");

        Assert.Multiple(() =>
        {
            Assert.That(a.Points, Is.EqualTo(3));
            Assert.That(a.Won, Is.EqualTo(1));
            Assert.That(a.Lost, Is.Zero);
            Assert.That(b.Points, Is.Zero);
            Assert.That(b.Lost, Is.EqualTo(1));
            Assert.That(b.Won, Is.Zero);
        });
    }

    [Test]
    public void Build_AwardsThreePointsForAnAwayWin()
    {
        var table = LeagueTable.Build(_league, _teams, [Played("A", "B", 0, 1)]);

        Assert.Multiple(() =>
        {
            Assert.That(table.Single(r => r.TeamId == "B").Points, Is.EqualTo(3));
            Assert.That(table.Single(r => r.TeamId == "B").Won, Is.EqualTo(1));
            Assert.That(table.Single(r => r.TeamId == "A").Points, Is.Zero);
        });
    }

    [Test]
    public void Build_AwardsOnePointEachForADraw()
    {
        var table = LeagueTable.Build(_league, _teams, [Played("A", "B", 1, 1)]);

        Assert.Multiple(() =>
        {
            Assert.That(table.Single(r => r.TeamId == "A").Points, Is.EqualTo(1));
            Assert.That(table.Single(r => r.TeamId == "B").Points, Is.EqualTo(1));
            Assert.That(table.Single(r => r.TeamId == "A").Drawn, Is.EqualTo(1));
            Assert.That(table.Single(r => r.TeamId == "B").Drawn, Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_AccumulatesGoalsForAndAgainstFromBothSidesOfTheFixture()
    {
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("A", "B", 3, 1),
            Played("B", "A", 2, 2)
        ]);

        var a = table.Single(r => r.TeamId == "A");
        var b = table.Single(r => r.TeamId == "B");

        Assert.Multiple(() =>
        {
            Assert.That(a.Played, Is.EqualTo(2));
            Assert.That(a.GoalsFor, Is.EqualTo(5));
            Assert.That(a.GoalsAgainst, Is.EqualTo(3));
            Assert.That(a.GoalDifference, Is.EqualTo(2));
            Assert.That(a.Points, Is.EqualTo(4));
            Assert.That(b.GoalsFor, Is.EqualTo(3));
            Assert.That(b.GoalsAgainst, Is.EqualTo(5));
            Assert.That(b.Points, Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_IgnoresUnplayedFixtures()
    {
        var pending = new Fixture { CompetitionId = "PL-COMP", HomeTeamId = "A", AwayTeamId = "B" };

        var table = LeagueTable.Build(_league, _teams, [pending]);

        Assert.That(table.Select(r => r.Played), Is.All.Zero);
    }

    [Test]
    public void Build_IgnoresAFixtureFlaggedPlayedWithoutAScore()
    {
        var missingScore = new Fixture
        {
            CompetitionId = "PL-COMP", HomeTeamId = "A", AwayTeamId = "B", IsPlayed = true
        };

        var table = LeagueTable.Build(_league, _teams, [missingScore]);

        Assert.That(table.Select(r => r.Played), Is.All.Zero);
    }

    [Test]
    public void Build_IgnoresFixturesInvolvingTeamsOutsideTheLeague()
    {
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("A", "OUTSIDER", 5, 0),
            Played("OUTSIDER", "B", 5, 0)
        ]);

        Assert.That(table.Select(r => r.Played), Is.All.Zero);
    }

    [Test]
    public void Build_OrdersByPointsThenGoalsForWhenGoalDifferenceIsLevel()
    {
        // C and D both take 3 points with a +2 difference; C scored more, so C is ahead.
        // A and B are both on 2 points at -2; A scored more, so A is ahead.
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("C", "A", 3, 1),
            Played("D", "B", 2, 0),
            Played("A", "B", 1, 1),
            Played("B", "A", 1, 1)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(table.Select(r => r.TeamId), Is.EqualTo(new[] { "C", "D", "A", "B" }));
            Assert.That(table[0].Points, Is.EqualTo(table[1].Points));
            Assert.That(table[0].GoalDifference, Is.EqualTo(table[1].GoalDifference));
            Assert.That(table[0].GoalsFor, Is.GreaterThan(table[1].GoalsFor));
        });
    }

    [Test]
    public void Build_OrdersByGoalDifferenceWhenPointsAreLevel()
    {
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("A", "B", 5, 0),
            Played("C", "D", 1, 0)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(table[0].TeamId, Is.EqualTo("A"));
            Assert.That(table[1].TeamId, Is.EqualTo("C"));
            Assert.That(table[0].Points, Is.EqualTo(table[1].Points));
        });
    }

    [Test]
    public void Build_BreaksACompleteTieOnTeamNameNotInsertionOrder()
    {
        var league = TestData.MakeLeague("PL", 1, ["D", "C", "B", "A"]);

        var table = LeagueTable.Build(league, _teams,
        [
            Played("A", "B", 1, 1),
            Played("C", "D", 1, 1)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(table.Select(r => r.TeamName), Is.Ordered);
            Assert.That(table.Select(r => r.TeamId), Is.EqualTo(new[] { "A", "B", "C", "D" }));
        });
    }

    [Test]
    public void Build_PutsTheHigherPointsTotalFirstEvenWithAWorseGoalDifference()
    {
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("A", "B", 1, 0),
            Played("C", "D", 9, 0),
            Played("D", "C", 9, 0)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(table[0].TeamId, Is.EqualTo("A"));
            Assert.That(table[0].Points, Is.EqualTo(3));
            Assert.That(table[0].GoalDifference, Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_SumsPlayedWonDrawnAndLostConsistently()
    {
        var table = LeagueTable.Build(_league, _teams,
        [
            Played("A", "B", 1, 0),
            Played("A", "C", 1, 1),
            Played("D", "A", 3, 0)
        ]);

        foreach (var row in table)
            Assert.That(row.Won + row.Drawn + row.Lost, Is.EqualTo(row.Played), row.TeamId);

        Assert.Multiple(() =>
        {
            Assert.That(table.Sum(r => r.Played), Is.EqualTo(6));
            Assert.That(table.Sum(r => r.GoalsFor), Is.EqualTo(table.Sum(r => r.GoalsAgainst)));
        });
    }

    [Test]
    public void Build_WithAnEmptyLeague_ReturnsAnEmptyTable()
    {
        var table = LeagueTable.Build(TestData.MakeLeague("EMPTY"), _teams, [Played("A", "B", 1, 0)]);

        Assert.That(table, Is.Empty);
    }

    [Test]
    public void Build_WithATeamMissingFromTheDictionary_Throws()
    {
        var league = TestData.MakeLeague("PL", 1, ["A", "GHOST"]);

        Assert.Throws<KeyNotFoundException>(() => LeagueTable.Build(league, _teams, []));
    }
}
