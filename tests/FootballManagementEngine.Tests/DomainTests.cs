using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class DomainTests
{
    [Test]
    public void StandingRow_GoalDifference_IsGoalsForMinusGoalsAgainst()
    {
        var row = new StandingRow { TeamId = "A", TeamName = "A", GoalsFor = 7, GoalsAgainst = 3 };

        Assert.That(row.GoalDifference, Is.EqualTo(4));
    }

    [Test]
    public void StandingRow_GoalDifference_CanBeNegative()
    {
        var row = new StandingRow { GoalsFor = 2, GoalsAgainst = 9 };

        Assert.That(row.GoalDifference, Is.EqualTo(-7));
    }

    [Test]
    public void CompetitionMatchRules_Defaults_AllowExtraTimeAndPenaltiesButNoReplay()
    {
        var rules = new CompetitionMatchRules();

        Assert.Multiple(() =>
        {
            Assert.That(rules.ReplayAllowed, Is.False);
            Assert.That(rules.ExtraTimeAllowed, Is.True);
            Assert.That(rules.PenaltiesAllowed, Is.True);
            Assert.That(rules.MaxReplays, Is.EqualTo(1));
        });
    }

    [Test]
    public void Team_Defaults_AreTheStartingClubFinancesAndFormation()
    {
        var team = new Team { Id = "A", Name = "A FC" };

        Assert.Multiple(() =>
        {
            Assert.That(team.Balance, Is.EqualTo(25_000_000m));
            Assert.That(team.WageBudgetWeekly, Is.EqualTo(500_000m));
            Assert.That(team.TransferBudget, Is.EqualTo(20_000_000m));
            Assert.That(team.Reputation, Is.EqualTo(70));
            Assert.That(team.Formation, Is.EqualTo(Formation.F442));
            Assert.That(team.Players, Is.Empty);
        });
    }

    [Test]
    public void GameState_Defaults_StartInJulyOfTheOpeningSeason()
    {
        var state = new GameState();

        Assert.Multiple(() =>
        {
            Assert.That(state.Season, Is.EqualTo(2026));
            Assert.That(state.CurrentDateUtc, Is.EqualTo(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)));
            Assert.That(state.CurrentDateUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(state.PlayerTeamId, Is.Null);
            Assert.That(state.Teams, Is.Empty);
            Assert.That(state.Fixtures, Is.Empty);
            Assert.That(state.PlayerStats, Is.Empty);
        });
    }

    [Test]
    public void Fixture_Id_IsAUniqueGuidByDefault()
    {
        var a = new Fixture();
        var b = new Fixture();

        Assert.Multiple(() =>
        {
            Assert.That(a.Id, Has.Length.EqualTo(32));
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(Guid.TryParseExact(a.Id, "N", out _), Is.True);
        });
    }

    [Test]
    public void Fixture_Defaults_AreAnUnplayedHomeGame()
    {
        var fixture = new Fixture();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.IsPlayed, Is.False);
            Assert.That(fixture.HomeGoals, Is.Null);
            Assert.That(fixture.AwayGoals, Is.Null);
            Assert.That(fixture.IsNeutralVenue, Is.False);
            Assert.That(fixture.Postponed, Is.False);
            Assert.That(fixture.ExtraTimePlayed, Is.False);
        });
    }

    [Test]
    public void Formation_SerialisesAsAStringEvenWithoutGlobalConverter()
    {
        // Formation carries [JsonConverter(typeof(JsonStringEnumConverter))] so clients always
        // see "F433" rather than an ordinal that would silently shift if the enum is reordered.
        var json = JsonSerializer.Serialize(new Team { Id = "A", Formation = Formation.F433 });

        Assert.That(json, Does.Contain("\"F433\""));
    }

    [Test]
    public void Formation_DeserialisesFromItsName()
    {
        var team = JsonSerializer.Deserialize<Team>("""{"Id":"A","Formation":"F532"}""");

        Assert.That(team!.Formation, Is.EqualTo(Formation.F532));
    }

    [Test]
    public void MatchHighlight_UsesCamelCaseJsonNames()
    {
        var json = JsonSerializer.Serialize(new MatchHighlight
        {
            Minute = 12,
            TeamId = "A",
            Type = MatchEventType.Goal,
            Description = "Goal!"
        }, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"minute\":12"));
            Assert.That(json, Does.Contain("\"teamId\":\"A\""));
            Assert.That(json, Does.Contain("\"type\":\"Goal\""));
            Assert.That(json, Does.Contain("\"description\":\"Goal!\""));
        });
    }

    [Test]
    public void MatchResult_Defaults_HaveNoHighlightsAndNoReplay()
    {
        var result = new MatchResult();

        Assert.Multiple(() =>
        {
            Assert.That(result.Highlights, Is.Empty);
            Assert.That(result.ReplayRequired, Is.False);
            Assert.That(result.ReplayFixtureId, Is.Null);
            Assert.That(result.HomePenalties, Is.Null);
            Assert.That(result.AwayPenalties, Is.Null);
        });
    }

    [Test]
    public void TransferOffer_DefaultsToAThreeYearContract()
    {
        Assert.That(new TransferOffer().ContractYears, Is.EqualTo(3));
    }

    [Test]
    public void ApiResponse_IsAValueRecord()
    {
        var expected = new ApiResponse(200, "{}");

        Assert.Multiple(() =>
        {
            Assert.That(new ApiResponse(200, "{}"), Is.EqualTo(expected));
            Assert.That(new ApiResponse(404, "{}"), Is.Not.EqualTo(expected));
        });
    }
}
