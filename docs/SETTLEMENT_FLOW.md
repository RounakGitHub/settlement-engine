# Settlement Flow

## Overview

Settlements use a **two-phase confirmation process** to ensure both parties explicitly agree before any balance changes are committed.

## State Machine

```
              ┌──────────┐
              │  (none)  │
              └────┬─────┘
                   │ User A proposes
                   ▼
              ┌──────────┐
              │ PROPOSED │
              └──┬───┬───┘
      User B     │   │     User A cancels
      confirms   │   │     or TTL expires
                 ▼   ▼
          ┌──────────┐  ┌──────────┐
          │CONFIRMED │  │CANCELLED │
          └──────────┘  └──────────┘
```

## Detailed Steps

### Step 1: Propose Settlement

```http
POST /api/groups/{groupId}/settlements
Content-Type: application/json

{
  "fromUserId": "user-a-id",
  "toUserId": "user-b-id",
  "amount": 1500.00,
  "note": "Settling dinner + cab from last week"
}
```

**System actions:**
1. Validate that the proposed amount aligns with current debt graph
2. Write `SettlementProposed` event to event store + outbox
3. Push real-time notification to User B via SignalR

### Step 2: Real-time Notification

User B receives a SignalR push:

```json
{
  "type": "SettlementProposed",
  "settlementId": "settlement-uuid",
  "fromUser": "User A",
  "amount": 1500.00,
  "note": "Settling dinner + cab from last week",
  "expiresAt": "2024-01-15T18:00:00Z"
}
```

### Step 3: Confirm Settlement

```http
POST /api/groups/{groupId}/settlements/{settlementId}/confirm
```

**System actions:**
1. Acquire distributed Redis lock: `settlement:{groupId}:{settlementId}`
   - Lock TTL: 30 seconds (prevents deadlocks)
   - If lock fails → return `409 Conflict` (concurrent confirmation attempt)
2. Validate settlement is still in `PROPOSED` state
3. Write `SettlementConfirmed` event to event store + outbox
4. Publish event to Kafka
5. Release Redis lock
6. Push live update to both users via SignalR

### Step 4: Balance Update

The projection worker consumes the `SettlementConfirmed` event and updates the denormalised read model. Both users see their updated balances in real-time.

## Race Condition Prevention

The Redis distributed lock prevents these scenarios:

| Scenario | Without Lock | With Lock |
|----------|-------------|-----------|
| User B confirms twice (double-tap) | Two `SettlementConfirmed` events | Second attempt gets `409 Conflict` |
| User A cancels while User B confirms | Inconsistent state | One wins, other gets `409` |
| Network retry triggers duplicate | Double settlement | Idempotency key + lock prevents duplicate |

## Cancellation

```http
POST /api/groups/{groupId}/settlements/{settlementId}/cancel
```

Only the proposer (User A) can cancel, and only while status is `PROPOSED`. Writes a `SettlementCancelled` event.
