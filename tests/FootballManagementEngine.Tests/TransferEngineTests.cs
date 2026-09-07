using NUnit.Framework;

namespace FootballManagementEngine.Tests;

[TestFixture]
public class TransferEngineTests
{
    private Team _seller = null!;
    private Team _buyer = null!;
    private Player _player = null!;

    [SetUp]
    public void SetUp()
    {
        _seller = TestData.MakeTeam("SELL", squadSize: 3, balance: 1_000_000m, transferBudget: 5_000_000m);
        _buyer = TestData.MakeTeam("BUY", squadSize: 3, balance: 10_000_000m, transferBudget: 8_000_000m);
        _player = _seller.Players[0];
    }

    [Test]
    public void Complete_MovesThePlayerBetweenSquads()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 2_000_000m, 25_000m, 4);

        Assert.Multiple(() =>
        {
            Assert.That(_seller.Players, Does.Not.Contain(_player));
            Assert.That(_buyer.Players, Does.Contain(_player));
            Assert.That(_seller.Players, Has.Count.EqualTo(2));
            Assert.That(_buyer.Players, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void Complete_MovesTheFeeFromBuyerToSeller()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 2_000_000m, 25_000m, 4);

        Assert.Multiple(() =>
        {
            Assert.That(_buyer.TransferBudget, Is.EqualTo(6_000_000m));
            Assert.That(_buyer.Balance, Is.EqualTo(8_000_000m));
            Assert.That(_seller.Balance, Is.EqualTo(3_000_000m));
            Assert.That(_seller.TransferBudget, Is.EqualTo(5_000_000m), "the fee is not added to the seller's budget");
        });
    }

    [Test]
    public void Complete_RewritesThePlayerContract()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 1m, 25_000m, 4);

        Assert.Multiple(() =>
        {
            Assert.That(_player.ContractClubId, Is.EqualTo("BUY"));
            Assert.That(_player.WeeklyWage, Is.EqualTo(25_000m));
            Assert.That(_player.ContractYears, Is.EqualTo(4));
        });
    }

    [Test]
    public void Complete_AllowsAFreeTransfer()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 0m, 0m, 1);

        Assert.Multiple(() =>
        {
            Assert.That(_buyer.Players, Does.Contain(_player));
            Assert.That(_buyer.TransferBudget, Is.EqualTo(8_000_000m));
            Assert.That(_player.WeeklyWage, Is.Zero);
        });
    }

    [Test]
    public void Complete_AllowsAFeeThatExactlyExhaustsTheBudget()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 8_000_000m, 10m, 2);

        Assert.That(_buyer.TransferBudget, Is.Zero);
    }

    [TestCase(-1, 1000, 3, TestName = "Complete_WithANegativeFee_Throws")]
    [TestCase(1000, -1, 3, TestName = "Complete_WithANegativeWage_Throws")]
    [TestCase(1000, 1000, 0, TestName = "Complete_WithZeroContractYears_Throws")]
    [TestCase(1000, 1000, -2, TestName = "Complete_WithNegativeContractYears_Throws")]
    public void Complete_WithInvalidTerms_Throws(int fee, int wage, int years)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TransferEngine.Complete(_seller, _buyer, _player, fee, wage, years));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Is.EqualTo("Invalid transfer terms."));
            Assert.That(_seller.Players, Does.Contain(_player));
        });
    }

    [Test]
    public void Complete_WhenTheBuyerCannotAffordTheFee_ThrowsAndChangesNothing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TransferEngine.Complete(_seller, _buyer, _player, 8_000_001m, 1_000m, 3));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("cannot afford"));
            Assert.That(_seller.Players, Does.Contain(_player), "the player must stay with the seller");
            Assert.That(_buyer.TransferBudget, Is.EqualTo(8_000_000m));
            Assert.That(_buyer.Balance, Is.EqualTo(10_000_000m));
            Assert.That(_seller.Balance, Is.EqualTo(1_000_000m));
        });
    }

    [Test]
    public void Complete_WhenThePlayerIsNotAtTheSellingClub_ThrowsAndChangesNothing()
    {
        var outsider = TestData.MakePlayer("OTHER-P01");

        var exception = Assert.Throws<InvalidOperationException>(
            () => TransferEngine.Complete(_seller, _buyer, outsider, 1_000m, 100m, 3));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("not registered"));
            Assert.That(_buyer.Players, Does.Not.Contain(outsider));
            Assert.That(_buyer.TransferBudget, Is.EqualTo(8_000_000m), "no money should move");
            Assert.That(outsider.ContractClubId, Is.Null);
        });
    }

    [Test]
    public void WeeklyFinanceUpdate_DeductsTheWholeWageBill()
    {
        var team = TestData.MakeTeam("A", squadSize: 5, wage: 10_000m, balance: 1_000_000m);

        TransferEngine.WeeklyFinanceUpdate([team]);

        Assert.That(team.Balance, Is.EqualTo(950_000m));
    }

    [Test]
    public void WeeklyFinanceUpdate_AppliesToEveryTeam()
    {
        var a = TestData.MakeTeam("A", squadSize: 2, wage: 1_000m, balance: 10_000m);
        var b = TestData.MakeTeam("B", squadSize: 4, wage: 500m, balance: 10_000m);

        TransferEngine.WeeklyFinanceUpdate([a, b]);

        Assert.Multiple(() =>
        {
            Assert.That(a.Balance, Is.EqualTo(8_000m));
            Assert.That(b.Balance, Is.EqualTo(8_000m));
        });
    }

    [Test]
    public void WeeklyFinanceUpdate_CanPushABalanceNegative()
    {
        var team = TestData.MakeTeam("A", squadSize: 11, wage: 100_000m, balance: 50_000m);

        TransferEngine.WeeklyFinanceUpdate([team]);

        Assert.That(team.Balance, Is.EqualTo(-1_050_000m));
    }

    [Test]
    public void WeeklyFinanceUpdate_WithNoPlayers_LeavesTheBalanceAlone()
    {
        var team = TestData.MakeTeam("A", squadSize: 0, balance: 12_345m);

        TransferEngine.WeeklyFinanceUpdate([team]);

        Assert.That(team.Balance, Is.EqualTo(12_345m));
    }

    [Test]
    public void WeeklyFinanceUpdate_WithNoTeams_DoesNothing()
    {
        Assert.DoesNotThrow(() => TransferEngine.WeeklyFinanceUpdate([]));
    }

    [Test]
    public void WeeklyFinanceUpdate_IsCumulativeAcrossWeeks()
    {
        var team = TestData.MakeTeam("A", squadSize: 2, wage: 1_000m, balance: 10_000m);

        TransferEngine.WeeklyFinanceUpdate([team]);
        TransferEngine.WeeklyFinanceUpdate([team]);
        TransferEngine.WeeklyFinanceUpdate([team]);

        Assert.That(team.Balance, Is.EqualTo(4_000m));
    }

    [Test]
    public void WeeklyFinanceUpdate_AfterATransfer_ChargesTheNewClub()
    {
        TransferEngine.Complete(_seller, _buyer, _player, 0m, 50_000m, 3);
        var sellerBalance = _seller.Balance;
        var buyerBalance = _buyer.Balance;

        TransferEngine.WeeklyFinanceUpdate([_seller, _buyer]);

        Assert.Multiple(() =>
        {
            Assert.That(sellerBalance - _seller.Balance, Is.EqualTo(2_000m), "seller keeps two 1,000 wages");
            Assert.That(buyerBalance - _buyer.Balance, Is.EqualTo(53_000m), "buyer pays three 1,000 wages plus 50,000");
        });
    }
}
