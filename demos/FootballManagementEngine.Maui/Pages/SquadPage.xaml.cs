using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

/// <summary>A squad member as the list draws them.</summary>
public sealed record PlayerRow(
    string PlayerId, string Position, string Name, string Status, string Badge, int Overall, int Age,
    int Appearances, int Goals, Color PositionColour, Color StatusColour, Color BadgeColour,
    bool IsListed, string Listing);

public partial class SquadPage : ContentPage
{
    private readonly GameSession _session;
    private readonly ObservableCollection<PlayerRow> _players = [];

    public SquadPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        SquadList.ItemsSource = _players;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _players.Clear();
        var club = _session.Club;
        if (club is null)
        {
            SquadHeader.Text = "Pick a club to see its squad";
            WageLabel.Text = "";
            return;
        }

        var squad = _session.Squad;
        SquadHeader.Text = $"{club.Name} · {squad.Count} players";
        WageLabel.Text =
            $"Wage bill {TransferMarket.Money(club.Players.Sum(p => p.WeeklyWage))}/week · Balance {TransferMarket.Money(club.Balance)}";

        foreach (var player in squad)
        {
            _players.Add(new PlayerRow(
                player.PlayerId,
                player.Position.ToString(),
                player.Name,
                Status(player),
                player.Badge,
                player.Overall,
                player.Age,
                player.Appearances,
                player.Goals,
                PositionColour(player.Position),
                player.Injured ? Color.FromArgb("#B3261E") : Colors.Gray,
                BadgeColour(player.State),
                player.ListedForTransfer,
                $"Transfer listed at {TransferMarket.Money(player.Value)}"));
        }
    }

    private async void OnPlayerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PlayerRow row) return;
        SquadList.SelectedItem = null;

        var confirmed = await DisplayAlertAsync(
            row.Name,
            row.IsListed
                ? "Take this player off the transfer list?"
                : "Put this player on the transfer list? Other clubs will be able to sign them at their value rather than at a premium.",
            row.IsListed ? "Remove" : "List",
            "Cancel");

        if (!confirmed) return;

        _session.ToggleTransferListed(row.PlayerId);
        OnAppearing();
    }

    private static string Status(SquadMember player) =>
        player.Injured
            ? $"Injured · {player.InjuryWeeks} week{(player.InjuryWeeks == 1 ? "" : "s")} out"
            : $"{TransferMarket.Money(player.WeeklyWage)}/week";

    private static Color BadgeColour(PlayerState state) => state switch
    {
        PlayerState.Selected => Color.FromArgb("#2F7D32"),
        PlayerState.Substitute => Color.FromArgb("#2F6FB3"),
        PlayerState.Injured => Color.FromArgb("#B3261E"),
        PlayerState.Suspended => Color.FromArgb("#C77700"),
        _ => Colors.Gray
    };

    private static Color PositionColour(Position position) => position switch
    {
        Position.GK => Color.FromArgb("#C77700"),
        Position.DEF => Color.FromArgb("#2F6FB3"),
        Position.MID => Color.FromArgb("#2F7D32"),
        _ => Color.FromArgb("#B3261E")
    };
}
