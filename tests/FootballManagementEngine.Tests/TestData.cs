namespace FootballManagementEngine.Tests;

/// <summary>Small, explicit builders so each test states only the data it cares about.</summary>
internal static class TestData
{
    public static Player MakePlayer(
        string id,
        Position position = Position.MID,
        int overall = 70,
        decimal wage = 1_000m,
        int shooting = 70,
        int goalkeeping = 60,
        string? clubId = null,
        int contractYears = 3)
        => new()
        {
            Id = id,
            Name = $"Player {id}",
            Position = position,
            Age = 24,
            Overall = overall,
            Potential = Math.Min(95, overall + 5),
            Pace = 70,
            Shooting = shooting,
            Passing = 70,
            Defending = 70,
            Goalkeeping = goalkeeping,
            WeeklyWage = wage,
            ContractClubId = clubId,
            ContractYears = contractYears
        };

    /// <summary>A squad of <paramref name="count"/> players; the first is always the goalkeeper.</summary>
    public static List<Player> MakeSquad(string teamId, int count = 11, int overall = 70, decimal wage = 1_000m)
        => Enumerable.Range(1, count)
            .Select(i => MakePlayer(
                $"{teamId}-P{i:00}",
                i == 1 ? Position.GK : i <= 5 ? Position.DEF : i <= 9 ? Position.MID : Position.FWD,
                overall,
                wage,
                clubId: teamId))
            .ToList();

    public static Team MakeTeam(
        string id,
        string leagueId = "PL",
        int squadSize = 11,
        int overall = 70,
        int reputation = 70,
        Formation formation = Formation.F442,
        decimal wage = 1_000m,
        decimal transferBudget = 20_000_000m,
        decimal balance = 25_000_000m,
        string? name = null)
        => new()
        {
            Id = id,
            Name = name ?? $"{id} FC",
            ShortName = id,
            LeagueId = leagueId,
            Players = MakeSquad(id, squadSize, overall, wage),
            Reputation = reputation,
            Formation = formation,
            TransferBudget = transferBudget,
            Balance = balance
        };

    public static League MakeLeague(
        string id = "PL",
        int level = 1,
        IEnumerable<string>? teamIds = null,
        int promotion = 0,
        int relegation = 0,
        int playoffs = 0)
    {
        var league = new League
        {
            Id = id,
            Name = $"{id} League",
            Level = level,
            PromotionSpots = promotion,
            RelegationSpots = relegation,
            PlayoffSpots = playoffs
        };
        if (teamIds != null) league.TeamIds.AddRange(teamIds);
        return league;
    }

    public static Competition MakeCompetition(
        string id,
        CompetitionType type = CompetitionType.League,
        string? leagueId = null,
        IEnumerable<string>? teamIds = null,
        CompetitionMatchRules? rules = null)
    {
        var competition = new Competition
        {
            Id = id,
            Name = $"{id} Competition",
            Type = type,
            LeagueId = leagueId,
            MatchRules = rules ?? new CompetitionMatchRules()
        };
        if (teamIds != null) competition.TeamIds.AddRange(teamIds);
        return competition;
    }

    public static Fixture MakeFixture(
        string competitionId,
        string homeTeamId,
        string awayTeamId,
        int round = 1,
        DateTime? dateUtc = null,
        string? tieId = null)
        => new()
        {
            CompetitionId = competitionId,
            Round = round,
            DateUtc = dateUtc ?? new DateTime(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            TieId = tieId
        };

    /// <summary>
    /// A four-team league ("PL"/"PL-COMP") with a full double round robin already generated.
    /// </summary>
    public static FootballGameEngine MakeLeagueWorld(
        int teamCount = 4,
        GamePersistence? persistence = null,
        bool autoSave = false,
        bool withFixtures = true)
    {
        var engine = new FootballGameEngine(persistence: persistence, autoSave: autoSave);
        var ids = Enumerable.Range(1, teamCount).Select(i => $"T{i}").ToList();

        foreach (var id in ids) engine.AddTeam(MakeTeam(id));

        engine.AddLeague(MakeLeague("PL", 1, ids, promotion: 1, relegation: 1));
        engine.AddCompetition(MakeCompetition("PL-COMP", CompetitionType.League, "PL", ids));

        if (withFixtures)
        {
            engine.State.Fixtures.AddRange(FixtureGenerator.DoubleRoundRobin(
                engine.State.Competitions["PL-COMP"],
                ids.Select(id => engine.State.Teams[id]).ToList(),
                new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc)));
        }

        return engine;
    }

    /// <summary>Records a result directly on a fixture, bypassing simulation.</summary>
    public static void RecordResult(FootballGameEngine engine, Fixture fixture, int homeGoals, int awayGoals)
        => engine.ApplyResult(new MatchResult
        {
            FixtureId = fixture.Id,
            HomeGoals = homeGoals,
            AwayGoals = awayGoals
        });

    /// <summary>
    /// Finds the lowest seed whose simulation satisfies <paramref name="predicate"/>. The engine
    /// creates its simulator with the same seed and options, so probing here is exact, not a guess.
    /// </summary>
    public static int FindSeed(
        Fixture fixture,
        Team home,
        Team away,
        MatchSimulationOptions options,
        Func<MatchResult, bool> predicate,
        int maxSeed = 20_000)
    {
        for (var seed = 1; seed <= maxSeed; seed++)
        {
            var result = new MatchSimulator(seed).Simulate(fixture, home, away, null, options);
            if (predicate(result)) return seed;
        }

        throw new InvalidOperationException($"No seed in 1..{maxSeed} satisfied the predicate.");
    }
}
