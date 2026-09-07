using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

/// <summary>One row of the league table, with the managed club highlighted.</summary>
public sealed record TableRow(
    int Position, string Name, int Played, int Won, int Drawn, int Lost,
    string GoalDifference, int Points, Color RowColour, Color TextColour, FontAttributes Emphasis);

public partial class TablePage : ContentPage
{
    private static readonly Color Highlight = Color.FromArgb("#12305B");
    private static readonly Color Transparent = Colors.Transparent;

    private readonly GameSession _session;
    private readonly ObservableCollection<TableRow> _rows = [];

    public TablePage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        TableList.ItemsSource = _rows;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _rows.Clear();
        var club = _session.Club;
        if (club is null)
        {
            DivisionLabel.Text = "Pick a club to see its division";
            return;
        }

        DivisionLabel.Text = $"{_session.LeagueName(club.LeagueId)} · {_session.Season}";

        var table = _session.Table;
        for (var i = 0; i < table.Count; i++)
        {
            var row = table[i];
            var mine = row.TeamId == club.Id;

            _rows.Add(new TableRow(
                i + 1,
                row.TeamName,
                row.Played, row.Won, row.Drawn, row.Lost,
                row.GoalDifference > 0 ? $"+{row.GoalDifference}" : row.GoalDifference.ToString(),
                row.Points,
                mine ? Highlight : Transparent,
                mine ? Colors.White : (Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black),
                mine ? FontAttributes.Bold : FontAttributes.None));
        }
    }
}
