using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

/// <summary>A squad member as the list draws them.</summary>
public sealed record PlayerRow(
    string Position, string Name, string Status, int Overall, int Age,
    int Appearances, int Goals, Color PositionColour, Color StatusColour);

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
            $"Wage bill {club.Players.Sum(p => p.WeeklyWage):C0}/week · Balance {club.Balance:C0}";

        foreach (var player in squad)
        {
            _players.Add(new PlayerRow(
                player.Position.ToString(),
                player.Name,
                Status(player),
                player.Overall,
                player.Age,
                player.Appearances,
                player.Goals,
                PositionColour(player.Position),
                player.Injured ? Color.FromArgb("#B3261E") : Colors.Gray));
        }
    }

    private static string Status(SquadMember player) =>
        player.Injured
            ? $"Injured · {player.InjuryWeeks} week{(player.InjuryWeeks == 1 ? "" : "s")} out"
            : $"{player.WeeklyWage:C0}/week";

    private static Color PositionColour(Position position) => position switch
    {
        Position.GK => Color.FromArgb("#C77700"),
        Position.DEF => Color.FromArgb("#2F6FB3"),
        Position.MID => Color.FromArgb("#2F7D32"),
        _ => Color.FromArgb("#B3261E")
    };
}
