using System.Globalization;

namespace FootballManagementEngine;

/// <summary>When clubs are allowed to trade.</summary>
public enum TransferWindow { Closed, Summer, Winter }

/// <summary>Why a bid was accepted or turned down.</summary>
public enum TransferOutcome
{
    Accepted,
    WindowClosed,
    PlayerNotFound,
    ClubNotFound,
    OwnPlayer,
    BidTooLow,
    WageTooLow,
    CannotAfford,
    SellingSquadTooSmall,
    BuyingSquadFull
}

/// <summary>The result of a bid, with a line of text a UI can show as-is.</summary>
public sealed record TransferResponse(TransferOutcome Outcome, string Message, decimal Fee = 0)
{
    public bool Accepted => Outcome == TransferOutcome.Accepted;
}

/// <summary>One player on the market, with everything a shortlist screen needs.</summary>
public sealed record TransferListing(
    string PlayerId,
    string PlayerName,
    Position Position,
    int Age,
    int Overall,
    int Potential,
    decimal WeeklyWage,
    string ClubId,
    string ClubName,
    string LeagueId,
    decimal Value,
    decimal AskingPrice,
    bool ListedByClub);

/// <summary>
/// Valuations, transfer windows and the rules a bid has to satisfy. Kept separate from
/// <see cref="TransferEngine"/>, which performs the paperwork once a deal is agreed.
/// </summary>
public static class TransferMarket
{
    /// <summary>
    /// Money is always written in pounds. Formatting with the ambient culture would print
    /// dollars on a device set to en-US, which is wrong for an English league.
    /// </summary>
    public static string Money(decimal amount) =>
        amount.ToString("C0", CultureInfo.GetCultureInfo("en-GB"));

    /// <summary>A club will not sell below this, so it can still field a side.</summary>
    public const int MinimumSquadSize = 16;

    /// <summary>Nor will a club buy beyond this.</summary>
    public const int MaximumSquadSize = 30;

    /// <summary>A club that has not listed a player wants a premium to part with them.</summary>
    public const decimal NotForSaleMultiplier = 1.6m;

    /// <summary>
    /// What a player is worth. Ability dominates, sharply - a top player costs many times what a
    /// merely good one does - then the age curve and any remaining potential adjust it.
    /// </summary>
    public static decimal Value(Player player)
    {
        var ability = Math.Pow(Math.Max(1, player.Overall) / 10.0, 4) * 2500;

        var ageFactor = player.Age switch
        {
            <= 20 => 1.30,
            <= 23 => 1.40,
            <= 28 => 1.20,
            <= 31 => 0.85,
            <= 34 => 0.50,
            _ => 0.25
        };

        // Unrealised potential is worth far more in a 19-year-old than in a 30-year-old.
        var upside = Math.Max(0, player.Potential - player.Overall) * (player.Age <= 23 ? 0.045 : 0.012);

        var value = ability * ageFactor * (1 + upside);
        return RoundToStep((decimal)value);
    }

    /// <summary>The asking price a club puts on a player, given whether they are listed.</summary>
    public static decimal AskingPrice(Player player, bool listedByClub) =>
        RoundToStep(listedByClub ? Value(player) : Value(player) * NotForSaleMultiplier);

    /// <summary>
    /// Summer runs from June to the end of August, winter for the month of January. Anything
    /// else is closed, so a manager cannot rebuild a squad in the middle of a run of fixtures.
    /// </summary>
    public static TransferWindow WindowFor(DateTime dateUtc) => dateUtc.Month switch
    {
        6 or 7 or 8 => TransferWindow.Summer,
        1 => TransferWindow.Winter,
        _ => TransferWindow.Closed
    };

    public static bool IsWindowOpen(DateTime dateUtc) => WindowFor(dateUtc) != TransferWindow.Closed;

    /// <summary>The wage a player expects: never a pay cut, and a rise for a bigger move.</summary>
    public static decimal ExpectedWage(Player player) => RoundWage(player.WeeklyWage * 1.15m);

    private static decimal RoundToStep(decimal value)
    {
        // Fees are quoted in tidy steps rather than to the pound.
        var step = value switch
        {
            < 1_000_000m => 25_000m,
            < 10_000_000m => 100_000m,
            _ => 500_000m
        };
        return Math.Max(step, Math.Round(value / step, MidpointRounding.AwayFromZero) * step);
    }

    private static decimal RoundWage(decimal wage) =>
        Math.Max(500m, Math.Round(wage / 500m, MidpointRounding.AwayFromZero) * 500m);
}
