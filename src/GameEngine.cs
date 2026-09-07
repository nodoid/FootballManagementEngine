using System.Text.Json;
using System.Text.Json.Serialization;

namespace FootballManagementEngine;

public sealed class FootballGameEngine
{
    public GameState State { get; }
    public GamePersistence? Persistence { get; }
    public bool AutoSave { get; }

    public JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ImportOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FootballGameEngine(GameState? state = null, GamePersistence? persistence = null, bool autoSave = false)
    {
        State = state ?? new GameState();
        Persistence = persistence;
        AutoSave = autoSave;
        InitialisePlayerStats();
        RefreshAllSelections();
    }

    public void Save(string slot = "default") =>
        (Persistence ?? throw new InvalidOperationException("No SQLite persistence has been configured.")).Save(this, slot);

    public bool SaveIfConfigured(string slot = "default")
    {
        if (Persistence == null) return false;
        Persistence.Save(this, slot);
        return true;
    }

    private void AutoSaveIfEnabled()
    {
        if (AutoSave && Persistence != null) Persistence.Save(this);
    }

    private void InitialisePlayerStats()
    {
        foreach (var team in State.Teams.Values)
            foreach (var player in team.Players)
            {
                if (!State.PlayerStats.ContainsKey(player.Id))
                    State.PlayerStats[player.Id] = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
            }
    }

    public void AddTeam(Team team)
    {
        State.Teams[team.Id] = team;
        foreach (var player in team.Players)
            if (!State.PlayerStats.ContainsKey(player.Id))
                State.PlayerStats[player.Id] = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
        RefreshSelection(team);
    }

    /// <summary>
    /// The eleven who will start for a club: the first eleven available players in squad order.
    /// Squad order is therefore team selection, and this is the single definition of it - the
    /// simulator's ratings, the appearance statistics and the Selected flag all read from here.
    /// </summary>
    public static IReadOnlyList<Player> StartingEleven(Team team)
    {
        // The first eleven in squad order are the intended side; that is what squad order means.
        var intended = team.Players.Take(11).ToList();
        var eleven = intended.Where(IsAvailable).ToList();

        var missing = intended.Where(p => !IsAvailable(p)).ToList();
        if (missing.Count == 0) return eleven;

        // Anyone unavailable is covered like for like where the squad allows it, so an injured
        // defender is replaced by a defender rather than by whoever happens to be next in line.
        var cover = team.Players.Skip(11).Where(IsAvailable).ToList();
        var taken = new HashSet<string>();

        foreach (var absentee in missing)
        {
            var replacement =
                cover.FirstOrDefault(p => !taken.Contains(p.Id) && p.Position == absentee.Position)
                ?? cover.FirstOrDefault(p => !taken.Contains(p.Id));

            if (replacement is null) break;
            taken.Add(replacement.Id);
            eleven.Add(replacement);
        }

        return eleven;
    }

    /// <summary>How many substitutes a club names alongside its starting eleven.</summary>
    public const int SubstituteCount = 4;

    /// <summary>
    /// The named substitutes: the next <see cref="SubstituteCount"/> available players in squad
    /// order who are not already starting. Anyone beyond them is a reserve, outside the squad.
    /// </summary>
    public static IReadOnlyList<Player> Substitutes(Team team)
    {
        var eleven = StartingEleven(team).Select(p => p.Id).ToHashSet();
        return team.Players
            .Where(p => IsAvailable(p) && !eleven.Contains(p.Id))
            .Take(SubstituteCount)
            .ToList();
    }

    private static bool IsAvailable(Player player) => !player.Injured && player.SuspensionMatches == 0;

    /// <summary>
    /// Brings every player's <see cref="Player.Selected"/> flag back in line with the current
    /// starting eleven. Called whenever availability changes, so a player who is injured out of
    /// the side is replaced by the next available squad member straight away.
    /// </summary>
    public static void RefreshSelection(Team team)
    {
        var eleven = StartingEleven(team).ToHashSet();
        var bench = Substitutes(team).ToHashSet();

        foreach (var player in team.Players)
        {
            player.Selected = eleven.Contains(player);
            player.Substitute = bench.Contains(player);
        }
    }

    /// <summary>Refreshes selection for every club, after a load or a week of recoveries.</summary>
    public void RefreshAllSelections()
    {
        foreach (var team in State.Teams.Values) RefreshSelection(team);
    }

    /// <summary>
    /// Selects the club managed by the human player.
    /// </summary>
    public Team SelectPlayerTeam(string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
            throw new ArgumentException("Team ID is required.", nameof(teamId));

        if (!State.Teams.TryGetValue(teamId, out var team))
            throw new KeyNotFoundException($"Team '{teamId}' not found.");

        State.PlayerTeamId = team.Id;
        AutoSaveIfEnabled();
        return team;
    }

    public Team? GetPlayerTeam() =>
        State.PlayerTeamId != null && State.Teams.TryGetValue(State.PlayerTeamId, out var team)
            ? team
            : null;
    public void AddLeague(League league) => State.Leagues[league.Id] = league;
    public void AddCompetition(Competition competition) =>
        State.Competitions[competition.Id] = competition;

    public void ResetSeasonPlayerStats()
    {
        State.PlayerStats.Clear();
        InitialisePlayerStats();
        AutoSaveIfEnabled();
    }

    public void SetPlayerInjury(string playerId, int weeks)
    {
        var player = State.Teams.Values.SelectMany(t => t.Players).SingleOrDefault(p => p.Id == playerId)
            ?? throw new KeyNotFoundException($"Player '{playerId}' not found.");
        ArgumentOutOfRangeException.ThrowIfNegative(weeks);
        player.Injured = weeks > 0;
        player.InjuryWeeks = weeks;
        if (weeks > 0 && State.PlayerStats.TryGetValue(player.Id, out var stats)) stats.Injuries++;

        var club = State.Teams.Values.FirstOrDefault(t => t.Players.Contains(player));
        if (club is not null) RefreshSelection(club);

        AutoSaveIfEnabled();
    }

    public void SetFormation(string teamId, Formation formation)
    {
        if (!State.Teams.TryGetValue(teamId, out var team))
            throw new KeyNotFoundException($"Team '{teamId}' not found.");
        team.Formation = formation;
        AutoSaveIfEnabled();
    }

    public Formation GetFormation(string teamId) =>
        State.Teams.TryGetValue(teamId, out var team)
            ? team.Formation
            : throw new KeyNotFoundException($"Team '{teamId}' not found.");

    /// <summary>
    /// Simulates and applies a fixture. Knockout rules are taken from the competition.
    /// A drawn replayable tie creates the next fixture; otherwise extra time and
    /// penalties are resolved according to the competition rules.
    /// </summary>
    public MatchResult SimulateFixture(
        string fixtureId,
        MatchSimulationOptions? options = null,
        int? seed = null)
    {
        var fixture = State.Fixtures.SingleOrDefault(f => f.Id == fixtureId)
            ?? throw new KeyNotFoundException($"Fixture '{fixtureId}' not found.");
        if (fixture.IsPlayed)
            throw new InvalidOperationException("Fixture already has a result.");

        if (!State.Teams.TryGetValue(fixture.HomeTeamId, out var home) ||
            !State.Teams.TryGetValue(fixture.AwayTeamId, out var away))
            throw new KeyNotFoundException("Fixture contains an unknown team.");

        if (!State.Competitions.TryGetValue(fixture.CompetitionId, out var competition))
            throw new KeyNotFoundException($"Competition '{fixture.CompetitionId}' not found.");

        var simulator = new MatchSimulator(seed ?? Environment.TickCount);
        var result = simulator.Simulate(fixture, home, away, competition.MatchRules, options);

        if (result.HomeGoals == result.AwayGoals &&
            competition.Type != CompetitionType.League &&
            competition.MatchRules.ReplayAllowed &&
            ReplayCount(fixture) < competition.MatchRules.MaxReplays)
        {
            var replay = new Fixture
            {
                CompetitionId = fixture.CompetitionId,
                Round = fixture.Round,
                DateUtc = fixture.DateUtc.AddDays(7),
                HomeTeamId = fixture.AwayTeamId,
                AwayTeamId = fixture.HomeTeamId,
                TieId = fixture.TieId ?? fixture.Id
            };

            State.Fixtures.Add(replay);
            result = new MatchResult
            {
                FixtureId = result.FixtureId,
                HomeGoals = result.HomeGoals,
                AwayGoals = result.AwayGoals,
                ExtraTime = false,
                DateUtc = result.DateUtc,
                DurationSeconds = result.DurationSeconds,
                Highlights = result.Highlights,
                ReplayRequired = true,
                ReplayFixtureId = replay.Id
            };
        }
        else if (result.HomeGoals == result.AwayGoals &&
                 competition.Type != CompetitionType.League)
        {
            result = ResolveKnockoutDraw(result, fixture, home, away, competition.MatchRules, options);
        }

        ApplyResult(result);
        return result;
    }

    private static MatchResult ResolveKnockoutDraw(
        MatchResult result, Fixture fixture, Team home, Team away,
        CompetitionMatchRules rules, MatchSimulationOptions? options)
    {
        var hg = result.HomeGoals;
        var ag = result.AwayGoals;
        var extraTime = false;
        var highlights = result.Highlights.ToList();

        if (rules.ExtraTimeAllowed)
        {
            extraTime = true;
            var et = new MatchSimulator(Environment.TickCount + 17)
                .Simulate(
                    fixture, home, away,
                    new CompetitionMatchRules(),
                    new MatchSimulationOptions
                    {
                        DurationSeconds = 0,
                        IncludeHighlights = options?.IncludeHighlights ?? true,
                        HighlightCount = 2,
                        MatchMinutes = 30
                    });
            hg += et.HomeGoals;
            ag += et.AwayGoals;
            highlights.AddRange(et.Highlights.Select(h => new MatchHighlight
            {
                Minute = h.Minute + 90,
                TeamId = h.TeamId,
                Type = h.Type == MatchEventType.Goal ? MatchEventType.Goal : h.Type,
                Description = $"Extra time: {h.Description}"
            }));
        }

        int? hp = null, ap = null;
        if (hg == ag && rules.PenaltiesAllowed)
        {
            (hp, ap) = PenaltyShootout(home, away);
            highlights.Add(new MatchHighlight
            {
                Minute = 120,
                Type = MatchEventType.PenaltyShootout,
                Description = $"{home.ShortName} {hp}–{ap} {away.ShortName} on penalties."
            });
        }

        return new MatchResult
        {
            FixtureId = result.FixtureId,
            HomeGoals = hg,
            AwayGoals = ag,
            ExtraTime = extraTime,
            HomePenalties = hp,
            AwayPenalties = ap,
            DateUtc = result.DateUtc,
            DurationSeconds = result.DurationSeconds,
            Highlights = highlights.OrderBy(h => h.Minute).ToList()
        };
    }

    private static (int Home, int Away) PenaltyShootout(Team home, Team away)
    {
        var rng = new Random(HashCode.Combine(home.Id, away.Id, DateTime.UtcNow.Ticks));
        var h = 0; var a = 0;
        for (var i = 0; i < 5; i++)
        {
            if (PenaltyScored(home, rng)) h++;
            if (PenaltyScored(away, rng)) a++;
        }
        var round = 0;
        while (h == a && round++ < 20)
        {
            if (PenaltyScored(home, rng)) h++;
            if (PenaltyScored(away, rng)) a++;
        }
        if (h == a) h++; // deterministic safety fallback
        return (h, a);
    }

    private static bool PenaltyScored(Team team, Random rng)
    {
        var keeper = team.Players.Where(p => p.Position == Position.GK).Select(p => p.Goalkeeping).DefaultIfEmpty(60).Average();
        var shooting = team.Players.Where(p => p.Position == Position.FWD || p.Position == Position.MID)
            .Select(p => p.Shooting).DefaultIfEmpty(60).Average();
        var chance = Math.Clamp(0.76 + (shooting - keeper) / 500.0, 0.65, 0.90);
        return rng.NextDouble() < chance;
    }

    private int ReplayCount(Fixture fixture)
    {
        var tieId = fixture.TieId ?? fixture.Id;
        return State.Fixtures.Count(f =>
            f.Id != fixture.Id &&
            f.CompetitionId == fixture.CompetitionId &&
            f.TieId == tieId);
    }

    public void ApplyResult(MatchResult result)
    {
        if (result.HomeGoals < 0 || result.AwayGoals < 0)
            throw new ArgumentException("Goals cannot be negative.");

        var fixture = State.Fixtures.SingleOrDefault(f => f.Id == result.FixtureId)
            ?? throw new KeyNotFoundException($"Fixture '{result.FixtureId}' not found.");

        if (fixture.IsPlayed)
            throw new InvalidOperationException("Fixture already has a result.");

        fixture.IsPlayed = true;
        fixture.HomeGoals = result.HomeGoals;
        fixture.AwayGoals = result.AwayGoals;
        fixture.ExtraTimePlayed = result.ExtraTime;
        fixture.HomePenalties = result.HomePenalties;
        fixture.AwayPenalties = result.AwayPenalties;
        UpdatePlayerStats(fixture, result);

        if (result.DateUtc.HasValue)
            fixture.DateUtc = result.DateUtc.Value;

        if (State.Teams.TryGetValue(fixture.HomeTeamId, out var homeTeam)) RefreshSelection(homeTeam);
        if (State.Teams.TryGetValue(fixture.AwayTeamId, out var awayTeam)) RefreshSelection(awayTeam);

        AutoSaveIfEnabled();
    }

    private void UpdatePlayerStats(Fixture fixture, MatchResult result)
    {
        if (!State.Teams.TryGetValue(fixture.HomeTeamId, out var home) || !State.Teams.TryGetValue(fixture.AwayTeamId, out var away)) return;
        UpdateTeamPlayerStats(home, result.HomeGoals, result.Highlights, fixture.AwayGoals.GetValueOrDefault());
        UpdateTeamPlayerStats(away, result.AwayGoals, result.Highlights, fixture.HomeGoals.GetValueOrDefault());
    }

    private void UpdateTeamPlayerStats(Team team, int goals, IReadOnlyList<MatchHighlight> highlights, int opponentGoals)
    {
        var starters = StartingEleven(team).ToList();
        if (starters.Count == 0) return;
        foreach (var player in starters)
        {
            if (!State.PlayerStats.TryGetValue(player.Id, out var stats))
                State.PlayerStats[player.Id] = stats = new PlayerSeasonStats { PlayerId = player.Id, TeamId = team.Id, Season = State.Season };
            stats.Appearances++; stats.Starts++; stats.Minutes += 90;
            // A clean sheet is conceding nothing, so it depends on the opponent's score.
            if (opponentGoals == 0) stats.CleanSheets++;
        }

        ApplyMatchInjuries(team, highlights);

        var goalHighlights = highlights.Where(h => h.Type == MatchEventType.Goal && h.TeamId == team.Id).ToList();
        var scorers = ScorerPool(starters);
        foreach (var goal in goalHighlights.Take(Math.Min(goals, goalHighlights.Count)))
        {
            var player = scorers[Math.Abs(goal.Minute) % scorers.Count];
            State.PlayerStats[player.Id].Goals++;
        }
        foreach (var card in highlights.Where(h => h.TeamId == team.Id && h.Type == MatchEventType.YellowCard))
        {
            var player = starters[Math.Abs(card.Minute) % starters.Count];
            State.PlayerStats[player.Id].YellowCards++;
        }
    }

    /// <summary>
    /// Turns the match's injury events into real, lasting injuries. The lay-off is derived from
    /// the minute rather than drawn at random, so a seeded match stays reproducible.
    /// </summary>
    private void ApplyMatchInjuries(Team team, IReadOnlyList<MatchHighlight> highlights)
    {
        foreach (var injury in highlights.Where(h => h.Type == MatchEventType.Injury && h.TeamId == team.Id))
        {
            var hurt = team.Players.FirstOrDefault(p => p.Id == injury.PlayerId);
            if (hurt is null || hurt.Injured) continue;

            hurt.Injured = true;
            hurt.InjuryWeeks = 1 + Math.Abs(injury.Minute) % 4;
            if (State.PlayerStats.TryGetValue(hurt.Id, out var stats)) stats.Injuries++;
        }
    }

    /// <summary>
    /// The players a goal can be credited to, each repeated according to how likely they are to
    /// score, so a forward is three times as likely as a defender and a goalkeeper never is.
    /// The scorer is then chosen by the goal's minute, which keeps attribution deterministic for
    /// a given seed while still spreading goals around the front line across a season.
    /// </summary>
    private static List<Player> ScorerPool(IReadOnlyList<Player> starters)
    {
        var pool = new List<Player>();
        foreach (var player in starters)
            for (var i = 0; i < GoalThreat(player.Position); i++)
                pool.Add(player);

        // A club with nobody but a goalkeeper available still has to credit its goals to someone.
        return pool.Count > 0 ? pool : [.. starters];
    }

    private static int GoalThreat(Position position) => position switch
    {
        Position.FWD => 3,
        Position.MID => 2,
        Position.DEF => 1,
        _ => 0
    };

    public void ApplyResultsJson(string json)
    {
        var results = JsonSerializer.Deserialize<List<MatchResult>>(json, JsonOptions)
            ?? throw new ArgumentException("Invalid results JSON.");

        foreach (var result in results)
            ApplyResult(result);
    }

    public List<StandingRow> GetLeagueTable(string leagueId)
    {
        var league = State.Leagues[leagueId];
        var competitionIds = State.Competitions.Values
            .Where(c => c.Type == CompetitionType.League && c.LeagueId == leagueId)
            .Select(c => c.Id)
            .ToHashSet();

        return LeagueTable.Build(
            league, State.Teams,
            State.Fixtures.Where(f => competitionIds.Contains(f.CompetitionId)));
    }

    public IEnumerable<Fixture> Fixtures(
        string? competitionId = null,
        string? teamId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        return State.Fixtures
            .Where(f => competitionId == null || f.CompetitionId == competitionId)
            .Where(f => teamId == null || f.HomeTeamId == teamId || f.AwayTeamId == teamId)
            .Where(f => fromUtc == null || f.DateUtc >= fromUtc)
            .Where(f => toUtc == null || f.DateUtc <= toUtc)
            .OrderBy(f => f.DateUtc);
    }

    // ---------- transfer market ----------

    public TransferWindow TransferWindow => TransferMarket.WindowFor(State.CurrentDateUtc);
    public bool IsTransferWindowOpen => TransferMarket.IsWindowOpen(State.CurrentDateUtc);

    /// <summary>Puts a player up for sale, which lowers what their club will accept.</summary>
    public void ListForTransfer(string playerId)
    {
        FindPlayer(playerId);
        State.TransferListed.Add(playerId);
        AutoSaveIfEnabled();
    }

    public void WithdrawFromTransferList(string playerId)
    {
        if (State.TransferListed.Remove(playerId)) AutoSaveIfEnabled();
    }

    public bool IsListedForTransfer(string playerId) => State.TransferListed.Contains(playerId);

    /// <summary>
    /// The market as a buying club sees it: every player at every other club, priced. Clubs that
    /// have listed a player want its value; the rest want a premium to be talked into selling.
    /// </summary>
    public IReadOnlyList<TransferListing> TransferMarketListings(
        string? excludeClubId = null,
        Position? position = null,
        decimal? maximumPrice = null,
        int? minimumOverall = null,
        string? search = null)
    {
        var results = new List<TransferListing>();

        foreach (var team in State.Teams.Values)
        {
            if (team.Id == excludeClubId) continue;
            // A club at the minimum squad size has nobody to spare.
            if (team.Players.Count <= TransferMarket.MinimumSquadSize) continue;

            foreach (var player in team.Players)
            {
                if (position is { } wanted && player.Position != wanted) continue;
                if (minimumOverall is { } floor && player.Overall < floor) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    player.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var listed = State.TransferListed.Contains(player.Id);
                var price = TransferMarket.AskingPrice(player, listed);
                if (maximumPrice is { } cap && price > cap) continue;

                results.Add(new TransferListing(
                    player.Id, player.Name, player.Position, player.Age, player.Overall, player.Potential,
                    player.WeeklyWage, team.Id, team.Name, team.LeagueId,
                    TransferMarket.Value(player), price, listed));
            }
        }

        return results
            .OrderByDescending(x => x.ListedByClub)
            .ThenByDescending(x => x.Overall)
            .ThenBy(x => x.AskingPrice)
            .ToList();
    }

    /// <summary>What a club would have to bid, and pay, to sign a given player.</summary>
    public TransferListing? QuoteFor(string playerId)
    {
        var (team, player) = FindPlayerOrDefault(playerId);
        if (team is null || player is null) return null;

        var listed = State.TransferListed.Contains(player.Id);
        return new TransferListing(
            player.Id, player.Name, player.Position, player.Age, player.Overall, player.Potential,
            player.WeeklyWage, team.Id, team.Name, team.LeagueId,
            TransferMarket.Value(player), TransferMarket.AskingPrice(player, listed), listed);
    }

    /// <summary>
    /// Bids for a player. Every rule is checked before any money or paperwork moves, so a
    /// rejected bid leaves both clubs exactly as they were.
    /// </summary>
    public TransferResponse Bid(string buyingClubId, string playerId, decimal fee, decimal weeklyWage, int contractYears = 3)
    {
        if (!IsTransferWindowOpen)
            return new TransferResponse(TransferOutcome.WindowClosed, "The transfer window is closed.");

        if (!State.Teams.TryGetValue(buyingClubId, out var buyer))
            return new TransferResponse(TransferOutcome.ClubNotFound, $"Club '{buyingClubId}' not found.");

        var (seller, player) = FindPlayerOrDefault(playerId);
        if (seller is null || player is null)
            return new TransferResponse(TransferOutcome.PlayerNotFound, $"Player '{playerId}' not found.");

        if (seller.Id == buyer.Id)
            return new TransferResponse(TransferOutcome.OwnPlayer, $"{player.Name} already plays for {buyer.Name}.");

        if (buyer.Players.Count >= TransferMarket.MaximumSquadSize)
            return new TransferResponse(TransferOutcome.BuyingSquadFull,
                $"{buyer.Name} already has {buyer.Players.Count} players.");

        if (seller.Players.Count <= TransferMarket.MinimumSquadSize)
            return new TransferResponse(TransferOutcome.SellingSquadTooSmall,
                $"{seller.Name} cannot go below {TransferMarket.MinimumSquadSize} players.");

        var asking = TransferMarket.AskingPrice(player, State.TransferListed.Contains(player.Id));
        if (fee < asking)
            return new TransferResponse(TransferOutcome.BidTooLow,
                $"{seller.Name} want {TransferMarket.Money(asking)} for {player.Name}.", asking);

        if (buyer.TransferBudget < fee)
            return new TransferResponse(TransferOutcome.CannotAfford,
                $"{buyer.Name} have {TransferMarket.Money(buyer.TransferBudget)} to spend.", asking);

        var expected = TransferMarket.ExpectedWage(player);
        if (weeklyWage < expected)
            return new TransferResponse(TransferOutcome.WageTooLow,
                $"{player.Name} wants {TransferMarket.Money(expected)} a week.", asking);

        TransferEngine.Complete(seller, buyer, player, fee, weeklyWage, contractYears);
        State.TransferListed.Remove(player.Id);

        if (!State.PlayerStats.ContainsKey(player.Id))
            State.PlayerStats[player.Id] = new PlayerSeasonStats { PlayerId = player.Id, TeamId = buyer.Id, Season = State.Season };

        RefreshSelection(seller);
        RefreshSelection(buyer);

        State.News.Add($"{State.CurrentDateUtc:d MMM yyyy}: {buyer.Name} sign {player.Name} from {seller.Name} for {TransferMarket.Money(fee)}.");
        AutoSaveIfEnabled();

        return new TransferResponse(TransferOutcome.Accepted,
            $"{buyer.Name} sign {player.Name} for {TransferMarket.Money(fee)}.", fee);
    }

    private Player FindPlayer(string playerId) =>
        FindPlayerOrDefault(playerId).Player
        ?? throw new KeyNotFoundException($"Player '{playerId}' not found.");

    private (Team? Team, Player? Player) FindPlayerOrDefault(string playerId)
    {
        foreach (var team in State.Teams.Values)
        {
            var player = team.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null) return (team, player);
        }
        return (null, null);
    }

    public string ExportState() => JsonSerializer.Serialize(State, JsonOptions);

    public static FootballGameEngine ImportState(string json, GamePersistence? persistence = null, bool autoSave = false)
    {
        var state = JsonSerializer.Deserialize<GameState>(json, ImportOptions)
            ?? throw new ArgumentException("Invalid save game.");

        return new FootballGameEngine(state, persistence, autoSave);
    }
}
