namespace SettlementEngine.Domain.Algorithms;

using SettlementEngine.Domain.Events;

/// <summary>
/// Minimum-edge debt reduction algorithm.
/// Computes the smallest number of transfers that clears all balances in a group.
/// </summary>
public static class DebtSimplifier
{
    /// <summary>
    /// Given a dictionary of net balances per user, returns the minimum set of transfers
    /// that settles all debts.
    /// </summary>
    /// <param name="netBalances">UserId → net balance (positive = creditor, negative = debtor)</param>
    /// <returns>Minimal list of transfers to settle all debts</returns>
    public static IReadOnlyList<SimplifiedTransfer> Simplify(Dictionary<Guid, decimal> netBalances)
    {
        var creditors = new List<(Guid UserId, decimal Amount)>();
        var debtors = new List<(Guid UserId, decimal Amount)>();

        foreach (var (userId, balance) in netBalances)
        {
            if (balance > 0.01m)
                creditors.Add((userId, balance));
            else if (balance < -0.01m)
                debtors.Add((userId, -balance)); // store as positive
        }

        // Sort descending by amount for greedy matching
        creditors.Sort((a, b) => b.Amount.CompareTo(a.Amount));
        debtors.Sort((a, b) => b.Amount.CompareTo(a.Amount));

        var transfers = new List<SimplifiedTransfer>();
        int ci = 0, di = 0;

        while (ci < creditors.Count && di < debtors.Count)
        {
            var creditor = creditors[ci];
            var debtor = debtors[di];

            var transferAmount = Math.Min(creditor.Amount, debtor.Amount);

            transfers.Add(new SimplifiedTransfer(
                FromUserId: debtor.UserId,
                ToUserId: creditor.UserId,
                Amount: Math.Round(transferAmount, 2)
            ));

            creditors[ci] = (creditor.UserId, creditor.Amount - transferAmount);
            debtors[di] = (debtor.UserId, debtor.Amount - transferAmount);

            if (creditors[ci].Amount < 0.01m) ci++;
            if (debtors[di].Amount < 0.01m) di++;
        }

        return transfers;
    }

    /// <summary>
    /// Computes net balances from a list of expense events.
    /// </summary>
    public static Dictionary<Guid, decimal> ComputeNetBalances(IEnumerable<ExpenseAdded> expenses)
    {
        var balances = new Dictionary<Guid, decimal>();

        foreach (var expense in expenses)
        {
            var splitCount = expense.SplitAmongUserIds.Count;
            if (splitCount == 0) continue;

            var sharePerPerson = expense.Amount / splitCount;

            // Payer is credited the full amount
            if (!balances.ContainsKey(expense.PaidByUserId))
                balances[expense.PaidByUserId] = 0;
            balances[expense.PaidByUserId] += expense.Amount;

            // Each participant (including payer) is debited their share
            foreach (var userId in expense.SplitAmongUserIds)
            {
                if (!balances.ContainsKey(userId))
                    balances[userId] = 0;
                balances[userId] -= sharePerPerson;
            }
        }

        return balances;
    }
}
