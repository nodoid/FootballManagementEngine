using System.Text.Json.Serialization;

namespace FootballManagementEngine;

public enum CompetitionType
{
    League,
    FaCup,
    LeagueCup,
    EflTrophy,
    EuropeanLeaguePhase,
    EuropeanKnockout
}

public enum Position { GK, DEF, MID, FWD }

public sealed class Player
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "";
    public Position Position { get; set; }
    public int Age { get; set; }
    public int Overall { get; set; }
    public int Potential { get; set; }
    public int Pace { get; set; }
    public int Shooting { get; set; }
    public int Passing { get; set; }
    public int Defending { get; set; }
    public int Goalkeeping { get; set; }
    public decimal WeeklyWage { get; set; }
    public string? ContractClubId { get; set; }
    public int ContractYears { get; set; }
    public bool Injured { get; set; }
    public int InjuryWeeks { get; set; }
    public int SuspensionMatches { get; set; }
}

public sealed class Team
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string LeagueId { get; set; } = "";
    public List<Player> Players { get; init; } = new();
    public decimal Balance { get; set; } = 25_000_000m;
    public decimal WageBudgetWeekly { get; set; } = 500_000m;
    public decimal TransferBudget { get; set; } = 20_000_000m;
    public int Reputation { get; set; } = 70;
    public string ManagerName { get; set; } = "Alex Manager";
}

public sealed class League
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Level { get; init; }
    public List<string> TeamIds { get; init; } = new();
    public int PromotionSpots { get; init; }
    public int RelegationSpots { get; init; }
    public int PlayoffSpots { get; init; }
}

public sealed class Competition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public CompetitionType Type { get; init; }
    public string? LeagueId { get; init; }
    public List<string> TeamIds { get; init; } = new();
}

public sealed class Fixture
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string CompetitionId { get; init; } = "";
    public int Round { get; init; }
    public DateTime DateUtc { get; set; }
    public string HomeTeamId { get; init; } = "";
    public string AwayTeamId { get; init; } = "";
    public bool IsNeutralVenue { get; init; }
    public bool IsPlayed { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public string? TieId { get; init; }
    public bool ExtraTimePlayed { get; set; }
    public int? HomePenalties { get; set; }
    public int? AwayPenalties { get; set; }
    public bool Postponed { get; set; }
}

public sealed class StandingRow
{
    public string TeamId { get; init; } = "";
    public string TeamName { get; init; } = "";
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points { get; set; }
}

public sealed class MatchResult
{
    [JsonPropertyName("fixtureId")] public string FixtureId { get; init; } = "";
    [JsonPropertyName("homeGoals")] public int HomeGoals { get; init; }
    [JsonPropertyName("awayGoals")] public int AwayGoals { get; init; }
    [JsonPropertyName("extraTime")] public bool ExtraTime { get; init; }
    [JsonPropertyName("homePenalties")] public int? HomePenalties { get; init; }
    [JsonPropertyName("awayPenalties")] public int? AwayPenalties { get; init; }
    [JsonPropertyName("dateUtc")] public DateTime? DateUtc { get; init; }
}

public sealed class TransferOffer
{
    public string PlayerId { get; init; } = "";
    public string SellingClubId { get; init; } = "";
    public string BuyingClubId { get; init; } = "";
    public decimal Fee { get; init; }
    public decimal WeeklyWage { get; init; }
    public int ContractYears { get; init; } = 3;
}

public sealed class GameState
{
    /// <summary>Team selected by the human manager. Null until a team is selected.</summary>
    public string? PlayerTeamId { get; set; }
    public int Season { get; set; } = 2026;
    public DateTime CurrentDateUtc { get; set; } = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    public Dictionary<string, Team> Teams { get; init; } = new();
    public Dictionary<string, League> Leagues { get; init; } = new();
    public Dictionary<string, Competition> Competitions { get; init; } = new();
    public List<Fixture> Fixtures { get; init; } = new();
    public List<string> News { get; init; } = new();
}
