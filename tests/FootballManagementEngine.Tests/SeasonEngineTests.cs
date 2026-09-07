using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class SeasonEngineTests
{
    private FootballGameEngine _engine = null!;
    private SeasonEngine _season = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = TestData.MakeLeagueWorld(withFixtures: false);
        _season = new SeasonEngine(_engine);
    }

    // ---------- GenerateDomesticSeason ----------

    [Test]
    public void GenerateDomesticSeason_BuildsADoubleRoundRobinForEveryLeague()
    {
        _season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.Fixtures, Has.Count.EqualTo(12));
            Assert.That(_engine.State.Fixtures.Select(f => f.CompetitionId), Is.All.EqualTo("PL-COMP"));
            Assert.That(_engine.State.Fixtures.Select(f => f.IsPlayed), Is.All.False);
        });
    }

    [Test]
    public void GenerateDomesticSeason_StartsTheCampaignInAugustOfTheCurrentSeason()
    {
        _engine.State.Season = 2030;

        _season.GenerateDomesticSeason();

        var opening = _engine.State.Fixtures.Min(f => f.DateUtc);

        Assert.Multiple(() =>
        {
            Assert.That(opening.Year, Is.EqualTo(2030));
            Assert.That(opening.Month, Is.EqualTo(8));
            Assert.That(opening.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday));
        });
    }

    [Test]
    public void GenerateDomesticSeason_CoversEveryLeagueInTheGame()
    {
        _engine.AddTeam(TestData.MakeTeam("C1", "CH"));
        _engine.AddTeam(TestData.MakeTeam("C2", "CH"));
        _engine.AddLeague(TestData.MakeLeague("CH", 2, ["C1", "C2"]));
        _engine.AddCompetition(TestData.MakeCompetition("CH-COMP", CompetitionType.League, "CH", ["C1", "C2"]));

        _season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "PL-COMP"), Is.EqualTo(12));
            Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "CH-COMP"), Is.EqualTo(2));
        });
    }

    [Test]
    public void GenerateDomesticSeason_ReplacesAnyExistingLeagueFixtures()
    {
        _season.GenerateDomesticSeason();
        var firstRun = _engine.State.Fixtures.Select(f => f.Id).ToList();

        _season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.Fixtures, Has.Count.EqualTo(12), "fixtures must not accumulate");
            Assert.That(_engine.State.Fixtures.Select(f => f.Id), Is.Not.EqualTo(firstRun));
        });
    }

    [Test]
    public void GenerateDomesticSeason_LeavesCupFixturesAlone()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));
        var cupTie = TestData.MakeFixture("FA", "T1", "T2");
        _engine.State.Fixtures.Add(cupTie);

        _season.GenerateDomesticSeason();

        Assert.That(_engine.State.Fixtures, Does.Contain(cupTie));
    }

    [Test]
    public void GenerateDomesticSeason_ClearsLastSeasonsPlayerStats()
    {
        _engine.State.PlayerStats["T1-P01"].Goals = 25;
        _engine.State.PlayerStats["T1-P01"].Appearances = 38;

        _season.GenerateDomesticSeason();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.PlayerStats["T1-P01"].Goals, Is.Zero);
            Assert.That(_engine.State.PlayerStats["T1-P01"].Appearances, Is.Zero);
        });
    }

    [Test]
    public void GenerateDomesticSeason_WithoutALeagueCompetition_Throws()
    {
        _engine.State.Competitions.Remove("PL-COMP");

        Assert.Throws<InvalidOperationException>(() => _season.GenerateDomesticSeason());
    }

    [Test]
    public void GenerateDomesticSeason_WithTwoCompetitionsForOneLeague_Throws()
    {
        _engine.AddCompetition(TestData.MakeCompetition("PL-COMP-2", CompetitionType.League, "PL"));

        Assert.Throws<InvalidOperationException>(() => _season.GenerateDomesticSeason());
    }

    [Test]
    public void GenerateDomesticSeason_WithNoLeagues_DoesNothing()
    {
        var empty = new SeasonEngine(new FootballGameEngine());

        Assert.DoesNotThrow(() => empty.GenerateDomesticSeason());
    }

    // ---------- GenerateFaCup ----------

    [Test]
    public void GenerateFaCup_DrawsAFirstRoundForEveryTeamInTheGame()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));

        _season.GenerateFaCup();

        var ties = _engine.State.Fixtures.Where(f => f.CompetitionId == "FA").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ties, Has.Count.EqualTo(2));
            Assert.That(ties.Select(f => f.Round), Is.All.EqualTo(1));
            Assert.That(ties.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count(), Is.EqualTo(4));
            Assert.That(ties.Select(f => f.TieId).Distinct().Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void GenerateFaCup_SchedulesTheRoundInSeptember()
    {
        _engine.State.Season = 2028;
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));

        _season.GenerateFaCup();

        Assert.That(
            _engine.State.Fixtures.Where(f => f.CompetitionId == "FA").Select(f => f.DateUtc).Distinct(),
            Is.EqualTo(new[] { new DateTime(2028, 9, 5, 15, 0, 0, DateTimeKind.Utc) }));
    }

    [Test]
    public void GenerateFaCup_ReplacesAnExistingDraw()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));
        _season.GenerateFaCup();

        _season.GenerateFaCup();

        Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "FA"), Is.EqualTo(2));
    }

    [Test]
    public void GenerateFaCup_LeavesLeagueFixturesAlone()
    {
        _engine.AddCompetition(TestData.MakeCompetition("FA", CompetitionType.FaCup));
        _season.GenerateDomesticSeason();

        _season.GenerateFaCup();

        Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "PL-COMP"), Is.EqualTo(12));
    }

    [Test]
    public void GenerateFaCup_WithoutTheFaCompetition_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _season.GenerateFaCup());
    }

    // ---------- GenerateEuropeanFixtures ----------

    [Test]
    public void GenerateEuropeanFixtures_BuildsALeaguePhaseForEachEuropeanCompetition()
    {
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "T2", "T3", "T4"]));

        _season.GenerateEuropeanFixtures();

        var fixtures = _engine.State.Fixtures.Where(f => f.CompetitionId == "UCL").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(8 * 2));
            Assert.That(fixtures.Select(f => f.Round).Distinct().Count(), Is.EqualTo(8));
        });
    }

    [Test]
    public void GenerateEuropeanFixtures_StartsInSeptember()
    {
        _engine.State.Season = 2027;
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "T2"]));

        _season.GenerateEuropeanFixtures();

        Assert.That(
            _engine.State.Fixtures.Min(f => f.DateUtc),
            Is.EqualTo(new DateTime(2027, 9, 16, 19, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void GenerateEuropeanFixtures_DeduplicatesRepeatedEntrants()
    {
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "T2", "T1", "T2"]));

        _season.GenerateEuropeanFixtures();

        var fixtures = _engine.State.Fixtures.Where(f => f.CompetitionId == "UCL").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixtures, Has.Count.EqualTo(8), "four entries but only two distinct clubs");
            Assert.That(fixtures.Any(f => f.HomeTeamId == f.AwayTeamId), Is.False);
        });
    }

    [Test]
    public void GenerateEuropeanFixtures_WithAnEmptyCompetition_ProducesNothing()
    {
        _engine.AddCompetition(TestData.MakeCompetition("UCL", CompetitionType.EuropeanLeaguePhase));

        _season.GenerateEuropeanFixtures();

        Assert.That(_engine.State.Fixtures, Is.Empty);
    }

    [Test]
    public void GenerateEuropeanFixtures_HandlesSeveralCompetitionsIndependently()
    {
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "T2"]));
        _engine.AddCompetition(TestData.MakeCompetition(
            "UEL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T3", "T4"]));

        _season.GenerateEuropeanFixtures();

        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "UCL"), Is.EqualTo(8));
            Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "UEL"), Is.EqualTo(8));
        });
    }

    [Test]
    public void GenerateEuropeanFixtures_ReplacesAnExistingLeaguePhase()
    {
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "T2"]));
        _season.GenerateEuropeanFixtures();

        _season.GenerateEuropeanFixtures();

        Assert.That(_engine.State.Fixtures.Count(f => f.CompetitionId == "UCL"), Is.EqualTo(8));
    }

    [Test]
    public void GenerateEuropeanFixtures_WithAnUnknownEntrant_Throws()
    {
        _engine.AddCompetition(TestData.MakeCompetition(
            "UCL", CompetitionType.EuropeanLeaguePhase, teamIds: ["T1", "GHOST"]));

        Assert.Throws<KeyNotFoundException>(() => _season.GenerateEuropeanFixtures());
    }

    [Test]
    public void GenerateEuropeanFixtures_IgnoresNonEuropeanCompetitions()
    {
        _season.GenerateEuropeanFixtures();

        Assert.That(_engine.State.Fixtures, Is.Empty);
    }

    // ---------- ProcessWeek ----------

    [Test]
    public void ProcessWeek_AdvancesTheCalendarBySevenDays()
    {
        var start = _engine.State.CurrentDateUtc;

        _season.ProcessWeek();

        Assert.That(_engine.State.CurrentDateUtc, Is.EqualTo(start.AddDays(7)));
    }

    [Test]
    public void ProcessWeek_PaysTheWageBill()
    {
        var start = _engine.State.Teams["T1"].Balance;

        _season.ProcessWeek();

        Assert.That(_engine.State.Teams["T1"].Balance, Is.EqualTo(start - 11_000m));
    }

    [Test]
    public void ProcessWeek_CountsDownAnInjury()
    {
        _engine.SetPlayerInjury("T1-P01", 3);

        _season.ProcessWeek();

        var player = _engine.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01");

        Assert.Multiple(() =>
        {
            Assert.That(player.InjuryWeeks, Is.EqualTo(2));
            Assert.That(player.Injured, Is.True);
        });
    }

    [Test]
    public void ProcessWeek_ReturnsAPlayerToFitnessOnTheFinalWeek()
    {
        _engine.SetPlayerInjury("T1-P01", 1);

        _season.ProcessWeek();

        var player = _engine.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01");

        Assert.Multiple(() =>
        {
            Assert.That(player.InjuryWeeks, Is.Zero);
            Assert.That(player.Injured, Is.False);
        });
    }

    [Test]
    public void ProcessWeek_DoesNotDriveInjuryWeeksNegative()
    {
        _engine.SetPlayerInjury("T1-P01", 1);

        _season.ProcessWeek();
        _season.ProcessWeek();

        Assert.That(_engine.State.Teams["T1"].Players.Single(p => p.Id == "T1-P01").InjuryWeeks, Is.Zero);
    }

    [Test]
    public void ProcessWeek_LeavesFitPlayersAlone()
    {
        _season.ProcessWeek();

        Assert.That(_engine.State.Teams.Values.SelectMany(t => t.Players).Select(p => p.Injured), Is.All.False);
    }

    [Test]
    public void ProcessWeek_WithoutPersistence_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _season.ProcessWeek());
    }

    [Test]
    public void ProcessWeek_RepeatedlyKeepsAdvancingTheSeason()
    {
        var start = _engine.State.CurrentDateUtc;

        for (var week = 0; week < 4; week++) _season.ProcessWeek();

        Assert.That(_engine.State.CurrentDateUtc, Is.EqualTo(start.AddDays(28)));
    }

    // ---------- PromoteAndRelegate ----------

    private (FootballGameEngine Engine, SeasonEngine Season) TwoLeagueWorld(
        int promotion = 1, int relegation = 1)
    {
        var engine = new FootballGameEngine();
        foreach (var id in new[] { "A1", "A2", "A3", "A4" }) engine.AddTeam(TestData.MakeTeam(id, "TOP"));
        foreach (var id in new[] { "B1", "B2", "B3", "B4" }) engine.AddTeam(TestData.MakeTeam(id, "BOT"));

        engine.AddLeague(TestData.MakeLeague("TOP", 1, ["A1", "A2", "A3", "A4"], relegation: relegation));
        engine.AddLeague(TestData.MakeLeague("BOT", 2, ["B1", "B2", "B3", "B4"], promotion: promotion));
        engine.AddCompetition(TestData.MakeCompetition("TOP-C", CompetitionType.League, "TOP"));
        engine.AddCompetition(TestData.MakeCompetition("BOT-C", CompetitionType.League, "BOT"));

        // A1 > A2 > A3 > A4 and B1 > B2 > B3 > B4 on points.
        AddWin(engine, "TOP-C", "A1", "A4");
        AddWin(engine, "TOP-C", "A1", "A3");
        AddWin(engine, "TOP-C", "A2", "A4");
        AddWin(engine, "TOP-C", "A2", "A3");
        AddWin(engine, "TOP-C", "A3", "A4");
        AddWin(engine, "BOT-C", "B1", "B4");
        AddWin(engine, "BOT-C", "B1", "B3");
        AddWin(engine, "BOT-C", "B2", "B4");
        AddWin(engine, "BOT-C", "B2", "B3");
        AddWin(engine, "BOT-C", "B3", "B4");

        return (engine, new SeasonEngine(engine));
    }

    private static void AddWin(FootballGameEngine engine, string competitionId, string winner, string loser)
    {
        var fixture = TestData.MakeFixture(competitionId, winner, loser);
        engine.State.Fixtures.Add(fixture);
        TestData.RecordResult(engine, fixture, 1, 0);
    }

    [Test]
    public void PromoteAndRelegate_SwapsTheBottomOfTheUpperLeagueWithTheTopOfTheLower()
    {
        var (engine, season) = TwoLeagueWorld();
        Assume.That(engine.GetLeagueTable("TOP").Last().TeamId, Is.EqualTo("A4"));
        Assume.That(engine.GetLeagueTable("BOT").First().TeamId, Is.EqualTo("B1"));

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues["TOP"].TeamIds, Does.Not.Contain("A4"));
            Assert.That(engine.State.Leagues["TOP"].TeamIds, Does.Contain("B1"));
            Assert.That(engine.State.Leagues["BOT"].TeamIds, Does.Contain("A4"));
            Assert.That(engine.State.Leagues["BOT"].TeamIds, Does.Not.Contain("B1"));
        });
    }

    [Test]
    public void PromoteAndRelegate_UpdatesTheLeagueOnEachTeam()
    {
        var (engine, season) = TwoLeagueWorld();

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams["A4"].LeagueId, Is.EqualTo("BOT"));
            Assert.That(engine.State.Teams["B1"].LeagueId, Is.EqualTo("TOP"));
            Assert.That(engine.State.Teams["A1"].LeagueId, Is.EqualTo("TOP"));
        });
    }

    [Test]
    public void PromoteAndRelegate_KeepsBothLeaguesTheSameSize()
    {
        var (engine, season) = TwoLeagueWorld(promotion: 2, relegation: 2);

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues["TOP"].TeamIds, Has.Count.EqualTo(4));
            Assert.That(engine.State.Leagues["BOT"].TeamIds, Has.Count.EqualTo(4));
            Assert.That(engine.State.Leagues["TOP"].TeamIds, Is.EquivalentTo(new[] { "A1", "A2", "B1", "B2" }));
            Assert.That(engine.State.Leagues["BOT"].TeamIds, Is.EquivalentTo(new[] { "B3", "B4", "A3", "A4" }));
        });
    }

    [Test]
    public void PromoteAndRelegate_WithNoSpots_ChangesNothing()
    {
        var (engine, season) = TwoLeagueWorld(promotion: 0, relegation: 0);

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues["TOP"].TeamIds, Is.EquivalentTo(new[] { "A1", "A2", "A3", "A4" }));
            Assert.That(engine.State.Leagues["BOT"].TeamIds, Is.EquivalentTo(new[] { "B1", "B2", "B3", "B4" }));
        });
    }

    [Test]
    public void PromoteAndRelegate_WithASingleLeague_ChangesNothing()
    {
        var season = new SeasonEngine(_engine);

        season.PromoteAndRelegate();

        Assert.That(_engine.State.Leagues["PL"].TeamIds, Has.Count.EqualTo(4));
    }

    [Test]
    public void PromoteAndRelegate_WithNoLeagues_DoesNotThrow()
    {
        var season = new SeasonEngine(new FootballGameEngine());

        Assert.DoesNotThrow(() => season.PromoteAndRelegate());
    }

    [Test]
    public void PromoteAndRelegate_CascadesThroughEveryTierByLevel()
    {
        var engine = new FootballGameEngine();
        foreach (var id in new[] { "A1", "A2", "B1", "B2", "C1", "C2" }) engine.AddTeam(TestData.MakeTeam(id));
        engine.AddLeague(TestData.MakeLeague("L3", 3, ["C1", "C2"], promotion: 1));
        engine.AddLeague(TestData.MakeLeague("L1", 1, ["A1", "A2"], relegation: 1));
        engine.AddLeague(TestData.MakeLeague("L2", 2, ["B1", "B2"], promotion: 1, relegation: 1));
        engine.AddCompetition(TestData.MakeCompetition("L1-C", CompetitionType.League, "L1"));
        engine.AddCompetition(TestData.MakeCompetition("L2-C", CompetitionType.League, "L2"));
        engine.AddCompetition(TestData.MakeCompetition("L3-C", CompetitionType.League, "L3"));
        AddWin(engine, "L1-C", "A1", "A2");
        AddWin(engine, "L2-C", "B1", "B2");
        AddWin(engine, "L3-C", "C1", "C2");

        new SeasonEngine(engine).PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues["L1"].TeamIds, Is.EquivalentTo(new[] { "A1", "B1" }));
            Assert.That(engine.State.Leagues["L3"].TeamIds, Does.Contain("C2"));
            Assert.That(engine.State.Teams["B1"].LeagueId, Is.EqualTo("L1"));
        });
    }

    [Test]
    public void PromoteAndRelegate_ThenRegeneratesAConsistentSeason()
    {
        var (engine, season) = TwoLeagueWorld();
        season.PromoteAndRelegate();
        engine.State.Season++;

        season.GenerateDomesticSeason();

        var topFixtures = engine.State.Fixtures.Where(f => f.CompetitionId == "TOP-C").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(topFixtures, Has.Count.EqualTo(12));
            Assert.That(topFixtures.Any(f => f.HomeTeamId == "B1" || f.AwayTeamId == "B1"), Is.True);
            Assert.That(topFixtures.Any(f => f.HomeTeamId == "A4" || f.AwayTeamId == "A4"), Is.False);
        });
    }
}

[TestFixture]
public class SeasonEnginePromotionCascadeTests
{
    /// <summary>
    /// Three tiers, each with one promotion and one relegation place. The middle tier holds three
    /// clubs so that a club arriving from above cannot be mistaken for the division's own bottom.
    /// </summary>
    private static (FootballGameEngine Engine, SeasonEngine Season) ThreeTierWorld()
    {
        var engine = new FootballGameEngine();
        foreach (var id in new[] { "A1", "A2", "B1", "B2", "B3", "C1", "C2" })
            engine.AddTeam(TestData.MakeTeam(id));

        engine.AddLeague(TestData.MakeLeague("L1", 1, ["A1", "A2"], relegation: 1));
        engine.AddLeague(TestData.MakeLeague("L2", 2, ["B1", "B2", "B3"], promotion: 1, relegation: 1));
        engine.AddLeague(TestData.MakeLeague("L3", 3, ["C1", "C2"], promotion: 1));
        engine.AddCompetition(TestData.MakeCompetition("L1-C", CompetitionType.League, "L1"));
        engine.AddCompetition(TestData.MakeCompetition("L2-C", CompetitionType.League, "L2"));
        engine.AddCompetition(TestData.MakeCompetition("L3-C", CompetitionType.League, "L3"));

        foreach (var (competition, winner, loser) in new[]
                 {
                     ("L1-C", "A1", "A2"),
                     ("L2-C", "B1", "B2"), ("L2-C", "B2", "B3"), ("L2-C", "B3", "B2"),
                     ("L3-C", "C1", "C2")
                 })
        {
            var fixture = TestData.MakeFixture(competition, winner, loser);
            engine.State.Fixtures.Add(fixture);
            TestData.RecordResult(engine, fixture, 2, 0);
        }

        return (engine, new SeasonEngine(engine));
    }

    [Test]
    public void PromoteAndRelegate_ARelegatedClubDropsExactlyOneTier()
    {
        var (engine, season) = ThreeTierWorld();

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams["A2"].LeagueId, Is.EqualTo("L2"));
            Assert.That(engine.State.Leagues["L2"].TeamIds, Does.Contain("A2"));
            Assert.That(engine.State.Leagues["L3"].TeamIds, Does.Not.Contain("A2"),
                "a club must not fall through two divisions in one pass");
        });
    }

    [Test]
    public void PromoteAndRelegate_SettlesEachTierOnItsOwnFinalTable()
    {
        // B2 finished bottom of the second tier on goal difference, so B2 goes down - not the
        // club that has just arrived from the tier above with no games played.
        var (engine, season) = ThreeTierWorld();

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams["B2"].LeagueId, Is.EqualTo("L3"));
            Assert.That(engine.State.Leagues["L2"].TeamIds, Is.EquivalentTo(new[] { "B3", "A2", "C1" }));
            Assert.That(engine.State.Leagues["L3"].TeamIds, Is.EquivalentTo(new[] { "C2", "B2" }));
        });
    }

    [Test]
    public void PromoteAndRelegate_KeepsEveryTierTheSameSizeWhenSpotsAreBalanced()
    {
        var (engine, season) = ThreeTierWorld();

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Leagues["L1"].TeamIds, Has.Count.EqualTo(2));
            Assert.That(engine.State.Leagues["L2"].TeamIds, Has.Count.EqualTo(3));
            Assert.That(engine.State.Leagues["L3"].TeamIds, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void PromoteAndRelegate_APromotedClubOnlyRisesOneTier()
    {
        var (engine, season) = ThreeTierWorld();

        season.PromoteAndRelegate();

        Assert.Multiple(() =>
        {
            Assert.That(engine.State.Teams["C1"].LeagueId, Is.EqualTo("L2"));
            Assert.That(engine.State.Teams["B1"].LeagueId, Is.EqualTo("L1"));
        });
    }

    [Test]
    public void PromoteAndRelegate_NeverLosesOrDuplicatesAClub()
    {
        var (engine, season) = ThreeTierWorld();

        season.PromoteAndRelegate();

        var members = engine.State.Leagues.Values.SelectMany(l => l.TeamIds).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(members, Has.Count.EqualTo(7));
            Assert.That(members, Is.Unique);
            Assert.That(members, Is.EquivalentTo(engine.State.Teams.Keys));
        });
    }
}
