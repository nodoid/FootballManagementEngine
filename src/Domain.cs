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

/// <summary>Supported tactical shapes. The shape changes the balance of attack, midfield and defence.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Formation
{
    F442,
    F433,
    F4231,
    F352,
    F343,
    F451,
    F4141,
    F532,
    F541,
    F41212
}

public sealed class CompetitionMatchRules
{
    /// <summary>Whether a drawn knockout match creates a replay instead of going straight to extra time.</summary>
    public bool ReplayAllowed { get; init; }
    public bool ExtraTimeAllowed { get; init; } = true;
    public bool PenaltiesAllowed { get; init; } = true;
    public int MaxReplays { get; init; } = 1;
}

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

public sealed class PlayerSeasonStats
{
    public string PlayerId { get; init; } = "";
    public string TeamId { get; init; } = "";
    public int Season { get; set; }
    public int Appearances { get; set; }
    public int Starts { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int CleanSheets { get; set; }
    public int Injuries { get; set; }
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
    /// <summary>The tactical formation used for this team's matches.</summary>
    public Formation Formation { get; set; } = Formation.F442;
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
    public CompetitionMatchRules MatchRules { get; init; } = new();
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

public enum MatchEventType { KickOff, Goal, Miss, Save, Chance, YellowCard, HalfTime, FullTime, ExtraTime, PenaltyShootout }

public sealed class MatchHighlight
{
    [JsonPropertyName("minute")] public int Minute { get; init; }
    [JsonPropertyName("teamId")] public string? TeamId { get; init; }
    [JsonPropertyName("type")] public MatchEventType Type { get; init; }
    [JsonPropertyName("description")] public string Description { get; init; } = "";
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
    [JsonPropertyName("replayRequired")] public bool ReplayRequired { get; init; }
    [JsonPropertyName("replayFixtureId")] public string? ReplayFixtureId { get; init; }
    [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; init; }
    [JsonPropertyName("highlights")] public IReadOnlyList<MatchHighlight> Highlights { get; init; } = Array.Empty<MatchHighlight>();
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
    /// <summary>Season aggregates are persisted so clients can query player statistics without rebuilding them.</summary>
    public Dictionary<string, PlayerSeasonStats> PlayerStats { get; init; } = new();
}
