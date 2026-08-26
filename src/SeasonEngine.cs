namespace FootballManagementEngine;

public sealed class SeasonEngine
{
    private readonly FootballGameEngine _game;
    private readonly Random _rng = new(20260825);

    public SeasonEngine(FootballGameEngine game) => _game = game;

    public void GenerateDomesticSeason()
    {
        foreach (var league in _game.State.Leagues.Values)
        {
            var competition = _game.State.Competitions.Values
                .Single(c => c.Type == CompetitionType.League && c.LeagueId == league.Id);

            _game.State.Fixtures.RemoveAll(f => f.CompetitionId == competition.Id);
            var teams = league.TeamIds.Select(id => _game.State.Teams[id]).ToList();
            _game.State.Fixtures.AddRange(
                FixtureGenerator.DoubleRoundRobin(
                    competition, teams,
                    new DateTime(_game.State.Season, 8, 1, 12, 0, 0, DateTimeKind.Utc)));
        }
    }

    public void GenerateFaCup()
    {
        var competition = _game.State.Competitions["FA"];
        var teams = _game.State.Teams.Values.ToList();

        // Creates an initial round. A production draw service can create subsequent
        // rounds from winners after results are recorded.
        _game.State.Fixtures.RemoveAll(f => f.CompetitionId == competition.Id);
        _game.State.Fixtures.AddRange(
            FixtureGenerator.CupRound(
                competition, teams, 1,
                new DateTime(_game.State.Season, 9, 5, 15, 0, 0, DateTimeKind.Utc),
                _rng));
    }

    public void GenerateEuropeanFixtures()
    {
        foreach (var competition in _game.State.Competitions.Values
                     .Where(c => c.Type == CompetitionType.EuropeanLeaguePhase))
        {
            var teams = competition.TeamIds
                .Distinct()
                .Select(id => _game.State.Teams[id])
                .ToList();

            _game.State.Fixtures.RemoveAll(f => f.CompetitionId == competition.Id);
            _game.State.Fixtures.AddRange(
                FixtureGenerator.EuropeanLeaguePhase(
                    competition, teams,
                    new DateTime(_game.State.Season, 9, 16, 19, 0, 0, DateTimeKind.Utc)));
        }
    }

    public void ProcessWeek()
    {
        TransferEngine.WeeklyFinanceUpdate(_game.State.Teams.Values);

        foreach (var player in _game.State.Teams.Values.SelectMany(t => t.Players))
        {
            if (player.Injured && player.InjuryWeeks > 0)
            {
                player.InjuryWeeks--;
                if (player.InjuryWeeks == 0) player.Injured = false;
            }
        }

        _game.State.CurrentDateUtc = _game.State.CurrentDateUtc.AddDays(7);
    }

    public void PromoteAndRelegate()
    {
        var leagues = _game.State.Leagues.Values.OrderBy(x => x.Level).ToList();

        for (int i = 0; i < leagues.Count - 1; i++)
        {
            var upper = leagues[i];
            var lower = leagues[i + 1];

            var upperTable = _game.GetLeagueTable(upper.Id);
            var lowerTable = _game.GetLeagueTable(lower.Id);

            var relegated = upperTable.TakeLast(upper.RelegationSpots).Select(x => x.TeamId).ToList();
            var promoted = lowerTable.Take(lower.PromotionSpots).Select(x => x.TeamId).ToList();

            foreach (var id in relegated)
            {
                upper.TeamIds.Remove(id);
                lower.TeamIds.Add(id);
                _game.State.Teams[id].LeagueId = lower.Id;
            }

            foreach (var id in promoted)
            {
                lower.TeamIds.Remove(id);
                upper.TeamIds.Add(id);
                _game.State.Teams[id].LeagueId = upper.Id;
            }
        }
    }
}
