using System.Collections.ObjectModel;
using FootballManagementEngine.Demo;

namespace FootballManagementEngine.Maui.Pages;

/// <summary>A fixture as the list draws it.</summary>
public sealed record FixtureRow(string Date, string Opponent, string Score, string Venue, Color ScoreColour);

/// <summary>One line of match commentary.</summary>
public sealed record HighlightRow(string Minute, string Description);

public partial class FixturesPage : ContentPage
{
    private static readonly Color Win = Color.FromArgb("#2F7D32");
    private static readonly Color Draw = Color.FromArgb("#8A6D1F");
    private static readonly Color Loss = Color.FromArgb("#B3261E");
    private static readonly Color Pending = Color.FromArgb("#8A8A8A");

    private readonly GameSession _session;
    private readonly ObservableCollection<FixtureRow> _fixtures = [];
    private readonly ObservableCollection<HighlightRow> _highlights = [];

    public FixturesPage(GameSession session)
    {
        InitializeComponent();
        _session = session;
        FixtureList.ItemsSource = _fixtures;
        HighlightList.ItemsSource = _highlights;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var club = _session.Club;
        NoClubLabel.IsVisible = club is null;
        NextCard.IsVisible = false;
        PlayButton.IsVisible = false;

        _fixtures.Clear();
        if (club is null) return;

        foreach (var fixture in _session.Fixtures)
        {
            _fixtures.Add(new FixtureRow(
                fixture.DateUtc.ToString("d MMM"),
                fixture.Opponent(club.Id),
                fixture.IsPlayed ? fixture.Score : "–",
                fixture.Venue(club.Id),
                ResultColour(fixture, club.Id)));
        }

        if (_session.NextFixture is { } next)
        {
            NextCard.IsVisible = true;
            PlayButton.IsVisible = true;
            NextCompetitionLabel.Text = $"{next.CompetitionName.ToUpperInvariant()} · ROUND {next.Round}";
            NextOpponentLabel.Text = next.IsHome(club.Id)
                ? $"v {next.Opponent(club.Id)}"
                : $"away at {next.Opponent(club.Id)}";
            NextDateLabel.Text = next.DateUtc.ToString("dddd d MMMM yyyy");
        }
        else
        {
            NextCompetitionLabel.Text = "";
            NextOpponentLabel.Text = "";
            NextDateLabel.Text = "";
        }
    }

    private async void OnPlayMatch(object? sender, EventArgs e)
    {
        var club = _session.Club;
        if (club is null || _session.NextFixture is not { } next) return;

        PlayButton.IsEnabled = false;
        PlayButton.Text = "Playing…";
        ResultCard.IsVisible = false;
        _highlights.Clear();

        var wasHome = next.IsHome(club.Id);
        var opponent = next.Opponent(club.Id);

        // The engine returns the whole match at once; the delay is purely so the demo reads
        // like a match unfolding rather than a number appearing instantly.
        var result = await Task.Run(() => _session.PlayNextMatch());

        if (result is not null)
        {
            var forGoals = wasHome ? result.HomeGoals : result.AwayGoals;
            var againstGoals = wasHome ? result.AwayGoals : result.HomeGoals;

            ResultCard.IsVisible = true;
            ResultScoreLabel.Text = $"{club.ShortName} {result.HomeGoals}–{result.AwayGoals} {opponent}";
            if (!wasHome) ResultScoreLabel.Text = $"{opponent} {result.HomeGoals}–{result.AwayGoals} {club.ShortName}";

            ResultVerdictLabel.Text = forGoals > againstGoals ? "Win" : forGoals < againstGoals ? "Defeat" : "Draw";

            foreach (var highlight in result.Highlights.OrderBy(h => h.Minute))
            {
                _highlights.Add(new HighlightRow($"{highlight.Minute}'", highlight.Description));
                await Task.Delay(120);
            }
        }

        PlayButton.IsEnabled = true;
        PlayButton.Text = "Play match";
        Refresh();
    }

    private static Color ResultColour(FixtureCard fixture, string teamId)
    {
        if (!fixture.IsPlayed || fixture.HomeGoals is null || fixture.AwayGoals is null) return Pending;

        var forGoals = fixture.IsHome(teamId) ? fixture.HomeGoals.Value : fixture.AwayGoals.Value;
        var againstGoals = fixture.IsHome(teamId) ? fixture.AwayGoals.Value : fixture.HomeGoals.Value;

        return forGoals > againstGoals ? Win : forGoals < againstGoals ? Loss : Draw;
    }
}
