# Debt Simplification Algorithm

## Problem Statement

In a group of N people, naive settlement can require up to N*(N-1)/2 individual transfers. But many debts cancel out transitively:

```
Example: A owes B ₹500, B owes C ₹500, C owes A ₹500
Naive: 3 transfers
Optimised: 0 transfers (all cancel out)
```

## Algorithm: Minimum-Edge Debt Reduction

### Step 1: Compute Net Balances

The `BalanceCalculator` computes net balances for each group member from PostgreSQL:

```
netBalance = sum(expense credits) - sum(expense splits) - sum(settlement payments out) + sum(settlement payments in)
```

All amounts are in **paise** (integer arithmetic) to avoid floating-point errors.

```
Example group after 5 expenses:
  Alice:  +200000  (net creditor, ₹2000)
  Bob:     -80000  (net debtor, ₹800)
  Charlie: -50000  (net debtor, ₹500)
  Diana:   +30000  (net creditor, ₹300)
  Eve:    -100000  (net debtor, ₹1000)
```

**Invariant**: Sum of all net balances = 0 (money is conserved).

### Step 2: Separate Creditors and Debtors

```
Creditors (positive): Alice (+200000), Diana (+30000)
Debtors (negative):   Eve (-100000), Bob (-80000), Charlie (-50000)
```

### Step 3: Greedy Matching

Sort both lists by absolute value (descending). Match the largest debtor with the largest creditor:

```
Round 1: Eve (-100000) → Alice (+200000)
         Transfer: Eve pays Alice ₹1000
         Remaining: Alice (+100000), Diana (+30000), Bob (-80000), Charlie (-50000)

Round 2: Bob (-80000) → Alice (+100000)
         Transfer: Bob pays Alice ₹800
         Remaining: Alice (+20000), Diana (+30000), Charlie (-50000)

Round 3: Charlie (-50000) → Diana (+30000)
         Transfer: Charlie pays Diana ₹300
         Remaining: Alice (+20000), Charlie (-20000)

Round 4: Charlie (-20000) → Alice (+20000)
         Transfer: Charlie pays Alice ₹200
         All balances zero.
```

**Result**: 4 transfers instead of up to 10 naive transfers.

### Complexity

- Time: O(N log N) for sorting + O(N) for matching = **O(N log N)**
- Space: O(N) for balance arrays

### Correctness Properties

1. **Conservation**: Total money transferred equals total debt
2. **Completeness**: All balances reach zero after simplification
3. **Minimality**: Transfer count is at most N-1 (where N = number of members with non-zero balances)
4. **Idempotency**: Running simplification twice on the same state produces the same result

## Implementation

### DebtSimplifier (`Splitr.Domain/Algorithms/DebtSimplifier.cs`)

Input: `Dictionary<Guid, long>` — net balance per user (in paise)
Output: `List<Transfer>` — minimum set of transfers

The algorithm:
1. Filters out zero-balance users
2. Separates into creditor (positive) and debtor (negative) lists
3. Sorts both by amount descending
4. Greedily matches largest debtor with largest creditor
5. Creates a transfer for `min(|debtor|, creditor)`, adjusts balances, repeats

### BalanceCalculator (`Splitr.Domain/Algorithms/BalanceCalculator.cs`)

Computes net balances by:
1. Summing expense `amount_paise` for each `paid_by` user (credits)
2. Summing expense split `amount_paise` for each split user (debits)
3. Subtracting confirmed settlement payer amounts
4. Adding confirmed settlement payee amounts

Only active (non-deleted) expenses and confirmed settlements are included.

## Concurrency Handling

The debt simplifier runs as a Kafka consumer (`DebtSimplifierConsumer`). Because expenses can be added concurrently:

- The simplifier always reads the **current full state** from PostgreSQL before computing
- It publishes a `DebtGraphUpdated` event with the optimised transfer set
- The consistency model is **eventual**: the simplified graph might be one event behind, and that's fine — it's presented as "recommended settlement as of now"
- The settlement plan query (`GetSettlementPlanQuery`) computes on-the-fly from the database for strong consistency when users request it

## Data Structures

### Input: Expense Events (trigger recomputation)

```json
{
  "eventType": "ExpenseAdded",
  "groupId": "uuid",
  "payload": {
    "expenseId": "uuid",
    "paidBy": "alice-id",
    "amountPaise": 100000,
    "splits": [
      { "userId": "alice-id", "amountPaise": 33334 },
      { "userId": "bob-id", "amountPaise": 33333 },
      { "userId": "charlie-id", "amountPaise": 33333 }
    ],
    "splitType": "Equal"
  }
}
```

### Output: Simplified Debt Graph

```json
{
  "eventType": "DebtGraphUpdated",
  "groupId": "uuid",
  "payload": {
    "transfers": [
      { "from": "eve-id", "to": "alice-id", "amountPaise": 100000 },
      { "from": "bob-id", "to": "alice-id", "amountPaise": 80000 },
      { "from": "charlie-id", "to": "diana-id", "amountPaise": 30000 },
      { "from": "charlie-id", "to": "alice-id", "amountPaise": 20000 }
    ]
  }
}
```

## Split Types

The system supports three split types:

| Type | Description |
|------|-------------|
| **Equal** | Amount divided equally among selected members (remainder distributed penny-by-penny) |
| **Exact** | Each member's share specified explicitly in paise |
| **Percentage** | Each member's share as a percentage, converted to paise |
