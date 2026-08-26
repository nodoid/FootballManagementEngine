namespace FootballManagementEngine;

public static class FixtureGenerator
{
    public static List<Fixture> DoubleRoundRobin(
        Competition competition,
        IReadOnlyList<Team> teams,
        DateTime startUtc,
        DayOfWeek day = DayOfWeek.Saturday)
    {
        var ids = teams.Select(x => x.Id).ToList();
        if (ids.Count < 2) return new();
        if (ids.Count % 2 != 0) ids.Add("BYE");

        var first = new List<Fixture>();
        var n = ids.Count;
        var cursor = Next(startUtc, day);

        for (var round = 0; round < n - 1; round++)
        {
            for (var i = 0; i < n / 2; i++)
            {
                var a = ids[i];
                var b = ids[n - 1 - i];
                if (a == "BYE" || b == "BYE") continue;

                var home = (round + i) % 2 == 0 ? a : b;
                var away = home == a ? b : a;

                first.Add(new Fixture
                {
                    CompetitionId = competition.Id,
                    Round = round + 1,
                    DateUtc = cursor,
                    HomeTeamId = home,
                    AwayTeamId = away
                });
            }
            ids = Rotate(ids);
            cursor = cursor.AddDays(7);
        }

        return first.Concat(first.Select(f => new Fixture
        {
            CompetitionId = f.CompetitionId,
            Round = f.Round + (n - 1),
            DateUtc = f.DateUtc.AddMonths(5),
            HomeTeamId = f.AwayTeamId,
            AwayTeamId = f.HomeTeamId
        })).ToList();
    }

    public static List<Fixture> CupRound(
        Competition competition,
        IReadOnlyList<Team> teams,
        int round,
        DateTime dateUtc,
        Random rng)
    {
        var ids = teams.Select(t => t.Id).OrderBy(_ => rng.Next()).ToList();
        var result = new List<Fixture>();

        for (int i = 0; i + 1 < ids.Count; i += 2)
        {
            result.Add(new Fixture
            {
                CompetitionId = competition.Id,
                Round = round,
                DateUtc = dateUtc,
                HomeTeamId = ids[i],
                AwayTeamId = ids[i + 1],
                TieId = $"{competition.Id}-{round}-{Guid.NewGuid():N}"
            });
        }
        return result;
    }

    public static List<Fixture> EuropeanLeaguePhase(
        Competition competition,
        IReadOnlyList<Team> teams,
        DateTime firstRoundUtc,
        int rounds = 8)
    {
        var ids = teams.Select(x => x.Id).ToList();
        if (ids.Count < 2) return new();
        if (ids.Count % 2 != 0) ids.Add("BYE");

        var n = ids.Count;
        var fixtures = new List<Fixture>();

        for (int round = 0; round < rounds; round++)
        {
            var rotated = ids.Skip(round % (n - 1))
                .Concat(ids.Take(round % (n - 1))).ToList();

            for (int i = 0; i < n / 2; i++)
            {
                var a = rotated[i];
                var b = rotated[n - 1 - i];
                if (a == "BYE" || b == "BYE") continue;

                var home = (round + i) % 2 == 0 ? a : b;
                var away = home == a ? b : a;

                fixtures.Add(new Fixture
                {
                    CompetitionId = competition.Id,
                    Round = round + 1,
                    DateUtc = firstRoundUtc.AddDays(round * 7),
                    HomeTeamId = home,
                    AwayTeamId = away
                });
            }
        }
        return fixtures;
    }

    private static List<string> Rotate(List<string> x) =>
        new System.Collections.Generic.List<string> { x[0], x[^1] }
            .Concat(x.Skip(1).Take(x.Count - 2)).ToList();

    private static DateTime Next(DateTime d, DayOfWeek target)
    {
        var delta = ((int)target - (int)d.DayOfWeek + 7) % 7;
        return d.Date.AddDays(delta).AddHours(15);
    }
}
