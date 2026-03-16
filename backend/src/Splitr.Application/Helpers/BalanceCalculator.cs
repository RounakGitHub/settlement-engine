using Splitr.Domain.Entities;

namespace Splitr.Application.Helpers;

public static class BalanceCalculator
{
    public static Dictionary<Guid, long> Compute(IEnumerable<Guid> memberIds, IReadOnlyList<Expense> expenses, IReadOnlyList<Settlement> confirmedSettlements)
    {
        var expenseCredits = expenses
            .GroupBy(e => e.PaidBy)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountPaise));

        var expenseDebits = expenses
            .SelectMany(e => e.Splits)
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.AmountPaise));

        var settlementDebits = confirmedSettlements
            .GroupBy(s => s.PayerId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.AmountPaise));

        var settlementCredits = confirmedSettlements
            .GroupBy(s => s.PayeeId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.AmountPaise));

        return memberIds.ToDictionary(
            id => id,
            id => expenseCredits.GetValueOrDefault(id) - expenseDebits.GetValueOrDefault(id) - settlementDebits.GetValueOrDefault(id) + settlementCredits.GetValueOrDefault(id)
        );
    }
}
