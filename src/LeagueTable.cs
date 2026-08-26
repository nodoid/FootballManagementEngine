namespace FootballManagementEngine;

public static class LeagueTable
{
    public static List<StandingRow> Build(
        League league,
        IReadOnlyDictionary<string, Team> teams,
        IEnumerable<Fixture> fixtures)
    {
        var rows = league.TeamIds.ToDictionary(
            id => id,
            id => new StandingRow
            {
                TeamId = id,
                TeamName = teams[id].Name
            });

        foreach (var f in fixtures.Where(x => x.IsPlayed && x.HomeGoals.HasValue && x.AwayGoals.HasValue))
        {
            if (!rows.TryGetValue(f.HomeTeamId, out var h) ||
                !rows.TryGetValue(f.AwayTeamId, out var a))
                continue;

            h.Played++; a.Played++;
            h.GoalsFor += f.HomeGoals!.Value;
            h.GoalsAgainst += f.AwayGoals!.Value;
            a.GoalsFor += f.AwayGoals!.Value;
            a.GoalsAgainst += f.HomeGoals!.Value;

            if (f.HomeGoals > f.AwayGoals) { h.Won++; a.Lost++; h.Points += 3; }
            else if (f.HomeGoals < f.AwayGoals) { a.Won++; h.Lost++; a.Points += 3; }
            else { h.Drawn++; a.Drawn++; h.Points++; a.Points++; }
        }

        return rows.Values
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor)
            .ThenBy(x => x.TeamName)
            .ToList();
    }
}
