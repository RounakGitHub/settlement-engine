using FluentAssertions;
using Splitr.Domain.Algorithms;
using Xunit;

namespace Splitr.Tests.Domain;

public class DebtSimplifierTests
{
    [Fact]
    public void Simplify_AllBalancesZero_ReturnsEmptyList()
    {
        var balances = new Dictionary<Guid, long>
        {
            { Guid.NewGuid(), 0 },
            { Guid.NewGuid(), 0 },
            { Guid.NewGuid(), 0 }
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_TwoPeople_SingleTransfer()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var balances = new Dictionary<Guid, long>
        {
            { userA, -5000 }, // A owes 50 INR
            { userB, 5000 }   // B is owed 50 INR
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().HaveCount(1);
        result[0].FromUserId.Should().Be(userA);
        result[0].ToUserId.Should().Be(userB);
        result[0].AmountPaise.Should().Be(5000);
    }

    [Fact]
    public void Simplify_CircularDebt_ResolvesToZeroTransfers()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        // A owes B 500, B owes C 500, C owes A 500 => net balances all zero
        var balances = new Dictionary<Guid, long>
        {
            { userA, 0 },
            { userB, 0 },
            { userC, 0 }
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_FourMembers_AtMostNMinus1Transfers()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var userD = Guid.NewGuid();

        var balances = new Dictionary<Guid, long>
        {
            { userA, 3000 },  // owed 30 INR
            { userB, -1000 }, // owes 10 INR
            { userC, -1500 }, // owes 15 INR
            { userD, -500 }   // owes 5 INR
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().HaveCountLessThanOrEqualTo(3); // N-1 = 3

        // Net transfers should balance out
        var totalPaid = result.Sum(t => t.AmountPaise);
        totalPaid.Should().Be(3000);
    }

    [Fact]
    public void Simplify_AllIntegerPaise_NoFloatingPoint()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        // 100 paise / 3 = 33, 33, 34 (remainder to payer)
        var balances = new Dictionary<Guid, long>
        {
            { userA, 67 },  // creditor
            { userB, -33 }, // debtor
            { userC, -34 }  // debtor
        };

        var result = DebtSimplifier.Simplify(balances);

        // All amounts must be exact integers
        foreach (var transfer in result)
        {
            transfer.AmountPaise.Should().BeGreaterThan(0);
            (transfer.AmountPaise % 1).Should().Be(0);
        }

        // Total debts paid = total credit
        result.Sum(t => t.AmountPaise).Should().Be(67);
    }

    [Fact]
    public void Simplify_EmptyBalances_ReturnsEmpty()
    {
        var result = DebtSimplifier.Simplify(new Dictionary<Guid, long>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_SinglePerson_ReturnsEmpty()
    {
        var balances = new Dictionary<Guid, long>
        {
            { Guid.NewGuid(), 0 }
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().BeEmpty();
    }
}
