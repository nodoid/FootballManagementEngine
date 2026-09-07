using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class TransferValuationTests
{
    private static Player Player(int overall, int age, int potential = 0) =>
        new()
        {
            Id = $"P{overall}-{age}",
            Name = "Test Player",
            Overall = overall,
            Age = age,
            Potential = potential == 0 ? overall : potential,
            WeeklyWage = 5_000m
        };

    [Test]
    public void Value_RisesSteeplyWithAbility()
    {
        var ordinary = TransferMarket.Value(Player(60, 26));
        var good = TransferMarket.Value(Player(75, 26));
        var excellent = TransferMarket.Value(Player(90, 26));

        Assert.Multiple(() =>
        {
            Assert.That(good, Is.GreaterThan(ordinary * 2));
            Assert.That(excellent, Is.GreaterThan(good * 2));
        });
    }

    [Test]
    public void Value_PeaksInAPlayersMidTwenties()
    {
        var prospect = TransferMarket.Value(Player(75, 21));
        var peak = TransferMarket.Value(Player(75, 26));
        var veteran = TransferMarket.Value(Player(75, 33));

        Assert.Multiple(() =>
        {
            Assert.That(prospect, Is.GreaterThan(peak), "youth carries a premium");
            Assert.That(veteran, Is.LessThan(peak));
            Assert.That(TransferMarket.Value(Player(75, 37)), Is.LessThan(veteran));
        });
    }

    [Test]
    public void Value_PaysForPotentialMostlyInTheYoung()
    {
        var youngWithUpside = TransferMarket.Value(Player(70, 20, potential: 90));
        var youngWithout = TransferMarket.Value(Player(70, 20, potential: 70));
        var oldWithUpside = TransferMarket.Value(Player(70, 30, potential: 90));
        var oldWithout = TransferMarket.Value(Player(70, 30, potential: 70));

        Assert.Multiple(() =>
        {
            Assert.That(youngWithUpside, Is.GreaterThan(youngWithout));
            Assert.That(youngWithUpside - youngWithout, Is.GreaterThan(oldWithUpside - oldWithout));
        });
    }

    [Test]
    public void Value_IsAlwaysPositiveAndQuotedInTidySteps()
    {
        foreach (var overall in new[] { 1, 40, 58, 70, 88, 99 })
        {
            var value = TransferMarket.Value(Player(overall, 26));
            Assert.That(value, Is.GreaterThan(0), $"overall {overall}");
            Assert.That(value % 25_000m, Is.Zero, $"overall {overall} is not a round figure");
        }
    }

    [Test]
    public void AskingPrice_IsHigherForAPlayerWhoIsNotListed()
    {
        var player = Player(75, 26);

        Assert.That(TransferMarket.AskingPrice(player, listedByClub: false),
            Is.GreaterThan(TransferMarket.AskingPrice(player, listedByClub: true)));
    }

    [Test]
    public void AskingPrice_ForAListedPlayer_IsTheirValue()
    {
        var player = Player(75, 26);

        Assert.That(TransferMarket.AskingPrice(player, listedByClub: true), Is.EqualTo(TransferMarket.Value(player)));
    }

    [Test]
    public void ExpectedWage_IsNeverAPayCut()
    {
        var player = Player(75, 26);

        Assert.That(TransferMarket.ExpectedWage(player), Is.GreaterThan(player.WeeklyWage));
    }

    [TestCase(6, TransferWindow.Summer)]
    [TestCase(7, TransferWindow.Summer)]
    [TestCase(8, TransferWindow.Summer)]
    [TestCase(1, TransferWindow.Winter)]
    [TestCase(2, TransferWindow.Closed)]
    [TestCase(5, TransferWindow.Closed)]
    [TestCase(9, TransferWindow.Closed)]
    [TestCase(12, TransferWindow.Closed)]
    public void WindowFor_FollowsTheFootballCalendar(int month, TransferWindow expected)
    {
        Assert.That(TransferMarket.WindowFor(new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Utc)),
            Is.EqualTo(expected));
    }
}

[TestFixture]
public class TransferMarketListingTests
{
    private FootballGameEngine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new FootballGameEngine();
        _engine.State.CurrentDateUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        _engine.AddLeague(TestData.MakeLeague("PL", 1, ["MINE", "THEM"]));
        _engine.AddTeam(TestData.MakeTeam("MINE", squadSize: 20));
        _engine.AddTeam(TestData.MakeTeam("THEM", squadSize: 20));
    }

    [Test]
    public void Listings_ExcludeTheBuyingClubsOwnPlayers()
    {
        var listings = _engine.TransferMarketListings(excludeClubId: "MINE");

        Assert.Multiple(() =>
        {
            Assert.That(listings, Is.Not.Empty);
            Assert.That(listings.Select(l => l.ClubId), Is.All.EqualTo("THEM"));
        });
    }

    [Test]
    public void Listings_SkipClubsWhoCannotAffordToLoseAnyone()
    {
        _engine.AddTeam(TestData.MakeTeam("SMALL", squadSize: TransferMarket.MinimumSquadSize));

        Assert.That(_engine.TransferMarketListings().Select(l => l.ClubId), Does.Not.Contain("SMALL"));
    }

    [Test]
    public void Listings_FilterByPosition()
    {
        var listings = _engine.TransferMarketListings(position: Position.FWD);

        Assert.Multiple(() =>
        {
            Assert.That(listings, Is.Not.Empty);
            Assert.That(listings.Select(l => l.Position), Is.All.EqualTo(Position.FWD));
        });
    }

    [Test]
    public void Listings_FilterByPriceAndAbility()
    {
        var cap = 2_000_000m;

        var listings = _engine.TransferMarketListings(maximumPrice: cap, minimumOverall: 60);

        Assert.Multiple(() =>
        {
            Assert.That(listings.Select(l => l.AskingPrice), Is.All.LessThanOrEqualTo(cap));
            Assert.That(listings.Select(l => l.Overall), Is.All.GreaterThanOrEqualTo(60));
        });
    }

    [Test]
    public void Listings_FilterByName()
    {
        var target = _engine.State.Teams["THEM"].Players[0];

        var listings = _engine.TransferMarketListings(search: target.Name);

        Assert.That(listings.Select(l => l.PlayerName), Is.All.EqualTo(target.Name));
    }

    [Test]
    public void Listings_PutTransferListedPlayersFirst()
    {
        var wanted = _engine.State.Teams["THEM"].Players[^1];
        _engine.ListForTransfer(wanted.Id);

        var listings = _engine.TransferMarketListings(excludeClubId: "MINE");

        Assert.Multiple(() =>
        {
            Assert.That(listings[0].PlayerId, Is.EqualTo(wanted.Id));
            Assert.That(listings[0].ListedByClub, Is.True);
        });
    }

    [Test]
    public void ListingAPlayer_LowersWhatTheirClubWillAccept()
    {
        var player = _engine.State.Teams["THEM"].Players[0];
        var before = _engine.QuoteFor(player.Id)!.AskingPrice;

        _engine.ListForTransfer(player.Id);
        var after = _engine.QuoteFor(player.Id)!.AskingPrice;

        Assert.Multiple(() =>
        {
            Assert.That(after, Is.LessThan(before));
            Assert.That(_engine.IsListedForTransfer(player.Id), Is.True);
        });
    }

    [Test]
    public void WithdrawingAPlayer_RestoresThePremium()
    {
        var player = _engine.State.Teams["THEM"].Players[0];
        _engine.ListForTransfer(player.Id);

        _engine.WithdrawFromTransferList(player.Id);

        Assert.Multiple(() =>
        {
            Assert.That(_engine.IsListedForTransfer(player.Id), Is.False);
            Assert.That(_engine.QuoteFor(player.Id)!.AskingPrice,
                Is.EqualTo(TransferMarket.AskingPrice(player, listedByClub: false)));
        });
    }

    [Test]
    public void ListForTransfer_WithAnUnknownPlayer_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _engine.ListForTransfer("GHOST"));
    }

    [Test]
    public void QuoteFor_AnUnknownPlayer_IsNull()
    {
        Assert.That(_engine.QuoteFor("GHOST"), Is.Null);
    }

    [Test]
    public void TransferList_SurvivesASaveAndLoad()
    {
        var player = _engine.State.Teams["THEM"].Players[0];
        _engine.ListForTransfer(player.Id);

        var restored = FootballGameEngine.ImportState(_engine.ExportState());

        Assert.That(restored.IsListedForTransfer(player.Id), Is.True);
    }
}

[TestFixture]
public class TransferBidTests
{
    private FootballGameEngine _engine = null!;
    private Team _buyer = null!;
    private Team _seller = null!;
    private Player _target = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new FootballGameEngine();
        _engine.State.CurrentDateUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        _buyer = TestData.MakeTeam("BUY", squadSize: 20, transferBudget: 50_000_000m);
        _seller = TestData.MakeTeam("SELL", squadSize: 20);
        _engine.AddTeam(_buyer);
        _engine.AddTeam(_seller);
        _target = _seller.Players[5];
    }

    private TransferResponse BidAtAsking(decimal? fee = null, decimal? wage = null)
    {
        var quote = _engine.QuoteFor(_target.Id)!;
        return _engine.Bid("BUY", _target.Id,
            fee ?? quote.AskingPrice,
            wage ?? TransferMarket.ExpectedWage(_target));
    }

    [Test]
    public void AnAcceptableBid_MovesThePlayerAndTheMoney()
    {
        var quote = _engine.QuoteFor(_target.Id)!;
        var buyerBudget = _buyer.TransferBudget;
        var sellerBalance = _seller.Balance;

        var response = BidAtAsking();

        Assert.Multiple(() =>
        {
            Assert.That(response.Accepted, Is.True, response.Message);
            Assert.That(_buyer.Players, Does.Contain(_target));
            Assert.That(_seller.Players, Does.Not.Contain(_target));
            Assert.That(_buyer.TransferBudget, Is.EqualTo(buyerBudget - quote.AskingPrice));
            Assert.That(_seller.Balance, Is.EqualTo(sellerBalance + quote.AskingPrice));
            Assert.That(_target.ContractClubId, Is.EqualTo("BUY"));
        });
    }

    [Test]
    public void AnAcceptedBid_ReportsTheSigningAsNews()
    {
        BidAtAsking();

        Assert.That(_engine.State.News, Has.Some.Contains(_target.Name));
    }

    [Test]
    public void AnAcceptedBid_RebuildsBothClubsSelections()
    {
        BidAtAsking();

        Assert.Multiple(() =>
        {
            Assert.That(_buyer.Players.Count(p => p.Selected), Is.EqualTo(11));
            Assert.That(_seller.Players.Count(p => p.Selected), Is.EqualTo(11));
            Assert.That(_engine.State.PlayerStats, Does.ContainKey(_target.Id));
        });
    }

    [Test]
    public void ABidBelowTheAskingPrice_IsRejectedAndChangesNothing()
    {
        var quote = _engine.QuoteFor(_target.Id)!;

        var response = BidAtAsking(fee: quote.AskingPrice - 25_000m);

        Assert.Multiple(() =>
        {
            Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.BidTooLow));
            Assert.That(response.Fee, Is.EqualTo(quote.AskingPrice), "the response quotes what they want");
            Assert.That(_seller.Players, Does.Contain(_target));
            Assert.That(_buyer.TransferBudget, Is.EqualTo(50_000_000m));
        });
    }

    [Test]
    public void AWageBelowWhatThePlayerWants_IsRejected()
    {
        var response = BidAtAsking(wage: 1m);

        Assert.Multiple(() =>
        {
            Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.WageTooLow));
            Assert.That(_seller.Players, Does.Contain(_target));
        });
    }

    [Test]
    public void ABidBeyondTheBudget_IsRejected()
    {
        _buyer.TransferBudget = 1_000m;

        var response = BidAtAsking();

        Assert.Multiple(() =>
        {
            Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.CannotAfford));
            Assert.That(_seller.Players, Does.Contain(_target));
        });
    }

    [Test]
    public void ABidOutsideTheWindow_IsRejected()
    {
        _engine.State.CurrentDateUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var response = BidAtAsking();

        Assert.Multiple(() =>
        {
            Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.WindowClosed));
            Assert.That(_engine.IsTransferWindowOpen, Is.False);
            Assert.That(_seller.Players, Does.Contain(_target));
        });
    }

    [Test]
    public void ABidForYourOwnPlayer_IsRejected()
    {
        var response = _engine.Bid("BUY", _buyer.Players[0].Id, 10_000_000m, 50_000m);

        Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.OwnPlayer));
    }

    [Test]
    public void ABidFromAFullSquad_IsRejected()
    {
        _buyer.Players.AddRange(TestData.MakeSquad("EXTRA", TransferMarket.MaximumSquadSize));

        var response = BidAtAsking();

        Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.BuyingSquadFull));
    }

    [Test]
    public void ABidThatWouldStripTheSellingSquad_IsRejected()
    {
        _seller.Players.RemoveRange(0, _seller.Players.Count - TransferMarket.MinimumSquadSize);
        var target = _seller.Players[0];

        var response = _engine.Bid("BUY", target.Id, 100_000_000m, 100_000m);

        Assert.Multiple(() =>
        {
            Assert.That(response.Outcome, Is.EqualTo(TransferOutcome.SellingSquadTooSmall));
            Assert.That(_seller.Players, Has.Count.EqualTo(TransferMarket.MinimumSquadSize));
        });
    }

    [Test]
    public void ABidForAnUnknownPlayer_IsRejected()
    {
        Assert.That(_engine.Bid("BUY", "GHOST", 1_000_000m, 10_000m).Outcome,
            Is.EqualTo(TransferOutcome.PlayerNotFound));
    }

    [Test]
    public void ABidFromAnUnknownClub_IsRejected()
    {
        Assert.That(_engine.Bid("GHOST", _target.Id, 1_000_000m, 10_000m).Outcome,
            Is.EqualTo(TransferOutcome.ClubNotFound));
    }

    [Test]
    public void SigningAListedPlayer_CostsLessAndClearsTheListing()
    {
        _engine.ListForTransfer(_target.Id);
        var listedPrice = _engine.QuoteFor(_target.Id)!.AskingPrice;

        var response = _engine.Bid("BUY", _target.Id, listedPrice, TransferMarket.ExpectedWage(_target));

        Assert.Multiple(() =>
        {
            Assert.That(response.Accepted, Is.True, response.Message);
            Assert.That(listedPrice, Is.LessThan(TransferMarket.AskingPrice(_target, listedByClub: false)));
            Assert.That(_engine.IsListedForTransfer(_target.Id), Is.False);
        });
    }

    [Test]
    public void ARejectedBid_LeavesTheSquadSizesAlone()
    {
        var buyerCount = _buyer.Players.Count;
        var sellerCount = _seller.Players.Count;

        BidAtAsking(fee: 1m);

        Assert.Multiple(() =>
        {
            Assert.That(_buyer.Players, Has.Count.EqualTo(buyerCount));
            Assert.That(_seller.Players, Has.Count.EqualTo(sellerCount));
        });
    }
}

[TestFixture]
public class TransferListOwnershipTests
{
    private FootballGameEngine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new FootballGameEngine();
        _engine.State.CurrentDateUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        _engine.AddTeam(TestData.MakeTeam("MINE", squadSize: 20));
        _engine.AddTeam(TestData.MakeTeam("THEM", squadSize: 20));
    }

    [Test]
    public void NoPlayerIsListedUntilSomebodyListsThem()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_engine.State.TransferListed, Is.Empty);
            Assert.That(_engine.TransferMarketListings().Select(l => l.ListedByClub), Is.All.False);
        });
    }

    [Test]
    public void PlayingAndAdvancingTheSeason_NeverListsAnyone()
    {
        _engine.AddLeague(TestData.MakeLeague("PL", 1, ["MINE", "THEM"]));
        _engine.AddCompetition(TestData.MakeCompetition("PL-COMP", CompetitionType.League, "PL", ["MINE", "THEM"]));
        var fixture = TestData.MakeFixture("PL-COMP", "MINE", "THEM");
        _engine.State.Fixtures.Add(fixture);

        _engine.SimulateFixture(fixture.Id, new MatchSimulationOptions { IncludeHighlights = false }, seed: 3);
        new SeasonEngine(_engine).ProcessWeek();

        Assert.That(_engine.State.TransferListed, Is.Empty);
    }

    [Test]
    public void SigningAPlayer_DoesNotLeaveThemListedAtTheirNewClub()
    {
        var target = _engine.State.Teams["THEM"].Players[3];
        _engine.ListForTransfer(target.Id);

        var response = _engine.Bid("MINE", target.Id,
            _engine.QuoteFor(target.Id)!.AskingPrice, TransferMarket.ExpectedWage(target));

        Assert.Multiple(() =>
        {
            Assert.That(response.Accepted, Is.True, response.Message);
            Assert.That(_engine.IsListedForTransfer(target.Id), Is.False);
            Assert.That(_engine.State.TransferListed, Is.Empty);
        });
    }

    [Test]
    public void AClubsOwnPlayersAreNeverOfferedToItself()
    {
        foreach (var player in _engine.State.Teams["MINE"].Players) _engine.ListForTransfer(player.Id);

        var market = _engine.TransferMarketListings(excludeClubId: "MINE");

        Assert.That(market.Select(l => l.ClubId), Does.Not.Contain("MINE"));
    }

    [Test]
    public void ListingIsReversible()
    {
        var player = _engine.State.Teams["MINE"].Players[0];

        _engine.ListForTransfer(player.Id);
        _engine.WithdrawFromTransferList(player.Id);

        Assert.That(_engine.State.TransferListed, Is.Empty);
    }
}
