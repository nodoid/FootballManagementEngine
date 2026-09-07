using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

/// <summary>One player on the market, as the list draws them.</summary>
public sealed record MarketRow(
    string PlayerId, string Position, string Name, string Detail, int Overall,
    string Price, Color PositionColour, Color PriceColour);

public partial class TransfersPage : ContentPage
{
    private const int PageSize = 200;

    private readonly GameSession _session;
    private readonly ObservableCollection<MarketRow> _rows = [];
    private readonly string[] _positions = ["Any", "GK", "DEF", "MID", "FWD"];

    public TransfersPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        MarketList.ItemsSource = _rows;
        PositionPicker.ItemsSource = _positions;
        PositionPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var club = _session.Club;
        var managing = club is not null;

        NoClubLabel.IsVisible = !managing;
        SummaryCard.IsVisible = managing;
        FilterRow.IsVisible = managing;

        _rows.Clear();
        if (club is null) return;

        WindowLabel.Text = _session.TransferWindowLabel;
        SquadLabel.Text = $"{club.Players.Count} players · wage bill {TransferMarket.Money(club.Players.Sum(p => p.WeeklyWage))}/week";
        BudgetLabel.Text = TransferMarket.Money(_session.TransferBudget);

        Position? position = PositionPicker.SelectedIndex switch
        {
            1 => FootballManagementEngine.Position.GK,
            2 => FootballManagementEngine.Position.DEF,
            3 => FootballManagementEngine.Position.MID,
            4 => FootballManagementEngine.Position.FWD,
            _ => null
        };

        var listings = _session.Market(position, search: MarketSearch.Text).Take(PageSize);

        foreach (var listing in listings)
        {
            var affordable = listing.AskingPrice <= _session.TransferBudget && _session.IsTransferWindowOpen;

            _rows.Add(new MarketRow(
                listing.PlayerId,
                listing.Position.ToString(),
                listing.PlayerName,
                $"{listing.ClubName} · {_session.LeagueName(listing.LeagueId)} · age {listing.Age}" +
                    (listing.ListedByClub ? " · transfer listed" : ""),
                listing.Overall,
                Compact(listing.AskingPrice),
                PositionColour(listing.Position),
                affordable ? Color.FromArgb("#2F7D32") : Colors.Gray));
        }
    }

    private void OnFilterChanged(object? sender, EventArgs e) => Refresh();

    private async void OnPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MarketRow row) return;
        MarketList.SelectedItem = null;

        if (_session.Quote(row.PlayerId) is not { } quote) return;

        var confirmed = await DisplayAlertAsync(
            $"Bid for {quote.PlayerName}?",
            $"{quote.ClubName} ({_session.LeagueName(quote.LeagueId)}) want {TransferMarket.Money(quote.AskingPrice)}.\n" +
            $"Wages {TransferMarket.Money(TransferMarket.ExpectedWage(new Player { WeeklyWage = quote.WeeklyWage }))} a week.\n" +
            $"Your budget is {TransferMarket.Money(_session.TransferBudget)}.",
            "Bid", "Cancel");

        if (!confirmed) return;

        var response = _session.SignPlayer(row.PlayerId);

        StatusLabel.Text = response.Message;
        StatusLabel.TextColor = response.Accepted ? Color.FromArgb("#2F7D32") : Color.FromArgb("#B3261E");
        StatusLabel.IsVisible = true;

        Refresh();
    }

    /// <summary>Fees read better as £12.5M than as a row of digits on a phone.</summary>
    private static string Compact(decimal amount) =>
        amount >= 1_000_000m ? $"£{amount / 1_000_000m:0.#}M"
        : amount >= 1_000m ? $"£{amount / 1_000m:0}K"
        : TransferMarket.Money(amount);

    private static Color PositionColour(Position position) => position switch
    {
        FootballManagementEngine.Position.GK => Color.FromArgb("#C77700"),
        FootballManagementEngine.Position.DEF => Color.FromArgb("#2F6FB3"),
        FootballManagementEngine.Position.MID => Color.FromArgb("#2F7D32"),
        _ => Color.FromArgb("#B3261E")
    };
}
