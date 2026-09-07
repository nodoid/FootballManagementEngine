using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

public partial class ClubPage : ContentPage
{
    private readonly GameSession _session;
    private readonly ObservableCollection<ClubGroup> _groups = [];
    private string _filter = "";

    public ClubPage(GameSession session)
    {
        InitializeComponent();
        _session = session;

        ClubList.ItemsSource = _groups;
        FormationPicker.ItemsSource = Enum.GetNames<Formation>()
            .Select(FormationLabel)
            .ToList();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BuildClubList();
        Refresh();
    }

    private void Refresh()
    {
        var club = _session.Club;
        var managing = club is not null;

        ClubCard.IsVisible = managing;
        AdvanceButton.IsVisible = managing;
        FormationRow.IsVisible = managing;
        PickerSection.IsVisible = !managing;

        if (club is null) return;

        var position = _session.TablePosition;
        ClubNameLabel.Text = club.Name;
        ClubLeagueLabel.Text = _session.LeagueName(club.LeagueId);
        ClubPositionLabel.Text = position > 0
            ? $"{Ordinal(position)} of {_session.Table.Count} · {_session.Table.FirstOrDefault(r => r.TeamId == club.Id)?.Points ?? 0} pts"
            : "Season not started";
        ClubDateLabel.Text = $"Season {_session.Season} · {_session.CurrentDateUtc:ddd d MMM yyyy}";

        var index = Array.IndexOf(Enum.GetValues<Formation>(), club.Formation);
        if (index >= 0 && FormationPicker.SelectedIndex != index) FormationPicker.SelectedIndex = index;
    }

    private void BuildClubList()
    {
        _groups.Clear();
        foreach (var division in _session.Divisions)
        {
            var clubs = _session.Clubs
                .Where(t => t.LeagueId == division.Id)
                .Where(t => _filter.Length == 0 || t.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (clubs.Count > 0) _groups.Add(new ClubGroup(division.Name, clubs));
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _filter = e.NewTextValue ?? "";
        BuildClubList();
    }

    private void OnClubSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Team team) return;

        ClubList.SelectedItem = null;
        _session.SelectClub(team.Id);
        Refresh();
    }

    private void OnFormationChanged(object? sender, EventArgs e)
    {
        if (_session.Club is null || FormationPicker.SelectedIndex < 0) return;

        var formation = Enum.GetValues<Formation>()[FormationPicker.SelectedIndex];
        if (formation != _session.Club.Formation) _session.SetFormation(formation);
    }

    private async void OnAdvanceWeek(object? sender, EventArgs e)
    {
        var transfers = _session.AdvanceWeek();
        Refresh();

        // A listed player may have been sold while the week passed.
        var mine = _session.Involving(transfers);
        if (mine.Count == 0) return;

        var lines = mine.Select(t => t.FromClubId == _session.Club?.Id
            ? $"{t.ToClubName} signed {t.PlayerName} for {TransferMarket.Money(t.Fee)}."
            : $"You signed {t.PlayerName} from {t.FromClubName} for {TransferMarket.Money(t.Fee)}.");

        await DisplayAlertAsync("Transfer news", string.Join("\n\n", lines), "OK");
    }

    /// <summary>Turns the enum name (F4231) into something readable (4-2-3-1).</summary>
    internal static string FormationLabel(string name) =>
        string.Join('-', name.TrimStart('F').ToCharArray());

    private static string Ordinal(int value) => value switch
    {
        11 or 12 or 13 => $"{value}th",
        _ when value % 10 == 1 => $"{value}st",
        _ when value % 10 == 2 => $"{value}nd",
        _ when value % 10 == 3 => $"{value}rd",
        _ => $"{value}th"
    };
}

/// <summary>A division and the clubs inside it, for the grouped club picker.</summary>
public sealed class ClubGroup(string name, IEnumerable<Team> clubs) : List<Team>(clubs)
{
    public string Name { get; } = name;
}
