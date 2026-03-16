namespace Splitr.Domain.Algorithms;

public record Transfer(Guid FromUserId, Guid ToUserId, long AmountPaise);

public static class DebtSimplifier
{
    /// <summary>
    /// Simplifies a set of net balances into the minimum number of transfers
    /// using a greedy min-edge reduction algorithm.
    /// Positive balance = creditor (is owed money).
    /// Negative balance = debtor (owes money).
    /// All amounts are in paise (integer arithmetic only).
    /// </summary>
    public static List<Transfer> Simplify(Dictionary<Guid, long> netBalances)
    {
        var creditors = netBalances
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => (Id: kvp.Key, Amount: kvp.Value))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var debtors = netBalances
            .Where(kvp => kvp.Value < 0)
            .Select(kvp => (Id: kvp.Key, Amount: -kvp.Value))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var transfers = new List<Transfer>();
        var ci = 0;
        var di = 0;

        while (ci < creditors.Count && di < debtors.Count)
        {
            var creditor = creditors[ci];
            var debtor = debtors[di];
            var settled = Math.Min(creditor.Amount, debtor.Amount);

            transfers.Add(new Transfer(debtor.Id, creditor.Id, settled));

            creditors[ci] = (creditor.Id, creditor.Amount - settled);
            debtors[di] = (debtor.Id, debtor.Amount - settled);

            if (creditors[ci].Amount == 0)
                ci++;
            if (debtors[di].Amount == 0)
                di++;
        }

        return transfers;
    }
}
