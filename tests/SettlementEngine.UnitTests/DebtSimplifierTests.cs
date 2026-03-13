using FluentAssertions;
using SettlementEngine.Domain.Algorithms;
using SettlementEngine.Domain.Events;

namespace SettlementEngine.UnitTests;

public class DebtSimplifierTests
{
    [Fact]
    public void Simplify_CircularDebt_ReturnsZeroTransfers()
    {
        // A owes B 500, B owes C 500, C owes A 500 → all cancel out
        var balances = new Dictionary<Guid, decimal>
        {
            [Guid.NewGuid()] = 0m,
            [Guid.NewGuid()] = 0m,
            [Guid.NewGuid()] = 0m
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_SingleDebt_ReturnsSingleTransfer()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var balances = new Dictionary<Guid, decimal>
        {
            [alice] = 500m,  // creditor
            [bob] = -500m    // debtor
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().HaveCount(1);
        result[0].FromUserId.Should().Be(bob);
        result[0].ToUserId.Should().Be(alice);
        result[0].Amount.Should().Be(500m);
    }

    [Fact]
    public void Simplify_FivePeople_ReducesTransfers()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var charlie = Guid.NewGuid();
        var diana = Guid.NewGuid();
        var eve = Guid.NewGuid();

        var balances = new Dictionary<Guid, decimal>
        {
            [alice] = 2000m,
            [bob] = -800m,
            [charlie] = -500m,
            [diana] = 300m,
            [eve] = -1000m
        };

        var result = DebtSimplifier.Simplify(balances);

        // Should be at most N-1 = 4 transfers (for 5 people with non-zero balances)
        result.Count.Should().BeLessOrEqualTo(4);

        // Net sum of all transfers should balance correctly
        var totalTransferred = result.Sum(t => t.Amount);
        totalTransferred.Should().Be(2300m); // total debt = 800 + 500 + 1000
    }

    [Fact]
    public void Simplify_PreservesMoneyConservation()
    {
        var users = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        var balances = new Dictionary<Guid, decimal>
        {
            [users[0]] = 1500m,
            [users[1]] = -300m,
            [users[2]] = -700m,
            [users[3]] = 200m,
            [users[4]] = -900m,
            [users[5]] = 200m
        };

        var result = DebtSimplifier.Simplify(balances);

        // After applying all transfers, all balances should be zero
        var finalBalances = new Dictionary<Guid, decimal>(balances);
        foreach (var transfer in result)
        {
            finalBalances[transfer.FromUserId] += transfer.Amount;
            finalBalances[transfer.ToUserId] -= transfer.Amount;
        }

        foreach (var balance in finalBalances.Values)
        {
            Math.Abs(balance).Should().BeLessThan(0.01m);
        }
    }

    [Fact]
    public void ComputeNetBalances_SingleExpense_SplitEqually()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var charlie = Guid.NewGuid();

        var expenses = new[]
        {
            new ExpenseAdded(
                EventId: Guid.NewGuid(),
                GroupId: Guid.NewGuid(),
                PaidByUserId: alice,
                Amount: 900m,
                Description: "Dinner",
                SplitAmongUserIds: [alice, bob, charlie],
                SplitType: "equal",
                IdempotencyKey: "key-1",
                OccurredAt: DateTime.UtcNow
            )
        };

        var balances = DebtSimplifier.ComputeNetBalances(expenses);

        balances[alice].Should().Be(600m);   // paid 900, owes 300 → net +600
        balances[bob].Should().Be(-300m);     // owes 300
        balances[charlie].Should().Be(-300m); // owes 300
    }
}
