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
        ActionRow.IsVisible = managing;
        FormationRow.IsVisible = managing;
        PickerPrompt.Text = managing ? "Switch club" : "Choose a club to manage";

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

    private void OnAdvanceWeek(object? sender, EventArgs e)
    {
        _session.AdvanceWeek();
        Refresh();
    }

    private async void OnChangeClub(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Change club",
            "Leave your current club and pick another? Results already played are kept.",
            "Change", "Stay");

        if (!confirmed) return;

        _session.Game.State.PlayerTeamId = null;
        _session.Game.SaveIfConfigured();
        Refresh();
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
