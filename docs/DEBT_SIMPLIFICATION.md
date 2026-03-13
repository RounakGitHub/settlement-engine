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

For each group member, calculate:

```
netBalance = sum(credits) - sum(debits)
```

```
Example group after 5 expenses:
  Alice:  +2000  (net creditor)
  Bob:    -800   (net debtor)
  Charlie: -500  (net debtor)
  Diana:  +300   (net creditor)
  Eve:    -1000  (net debtor)
```

**Invariant**: Sum of all net balances = 0 (money is conserved).

### Step 2: Separate Creditors and Debtors

```
Creditors (positive): Alice (+2000), Diana (+300)
Debtors (negative):   Eve (-1000), Bob (-800), Charlie (-500)
```

### Step 3: Greedy Matching

Sort both lists by absolute value (descending). Match the largest debtor with the largest creditor:

```
Round 1: Eve (-1000) → Alice (+2000)
         Transfer: Eve pays Alice ₹1000
         Remaining: Alice (+1000), Diana (+300), Bob (-800), Charlie (-500)

Round 2: Bob (-800) → Alice (+1000)
         Transfer: Bob pays Alice ₹800
         Remaining: Alice (+200), Diana (+300), Charlie (-500)

Round 3: Charlie (-500) → Diana (+300)
         Transfer: Charlie pays Diana ₹300
         Remaining: Alice (+200), Charlie (-200)

Round 4: Charlie (-200) → Alice (+200)
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

## Concurrency Handling

The debt simplifier runs as a Kafka consumer. Because expenses can be added concurrently:

- The simplifier always reads the **current full debt state** for the group before computing
- It publishes a `DebtGraphUpdated` event with version metadata
- The read model only applies updates with a version newer than its current state
- The consistency model is **eventual**: the simplified graph might be one event behind, and that's fine — it's presented as "recommended settlement as of now"

## Data Structure

### Input: Expense Events

```json
{
  "groupId": "uuid",
  "paidBy": "alice-id",
  "amount": 1000.00,
  "splitAmong": ["alice-id", "bob-id", "charlie-id"],
  "splitType": "equal"
}
```

### Output: Simplified Debt Graph

```json
{
  "groupId": "uuid",
  "version": 42,
  "transfers": [
    { "from": "eve-id", "to": "alice-id", "amount": 1000.00 },
    { "from": "bob-id", "to": "alice-id", "amount": 800.00 },
    { "from": "charlie-id", "to": "diana-id", "amount": 300.00 },
    { "from": "charlie-id", "to": "alice-id", "amount": 200.00 }
  ],
  "computedAt": "2024-01-15T12:00:00Z"
}
```

## Testing the Algorithm

Key properties to verify in tests:

- No expense is double-counted
- No settlement is confirmed twice
- Balances always converge to correct values under concurrent expense submission
- The simplified transfer count is always ≤ the naive transfer count
- Net balance conservation: sum of all balances = 0 after every operation
