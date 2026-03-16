# Settlement Flow

## Overview

Settlements use **Razorpay payment integration** with webhook-driven confirmation. The payer initiates a settlement which creates a Razorpay order, completes payment via the Razorpay UI, and the system confirms the settlement automatically when Razorpay sends a payment webhook.

## State Machine

```
              ┌──────────┐
              │  (none)  │
              └────┬─────┘
                   │ User A initiates
                   │ (Razorpay order created)
                   v
              ┌──────────┐
              │ PENDING  │
              └──┬─┬─┬───┘
                 │ │ │
    ┌────────────┘ │ └────────────┐
    │              │              │
    │  Razorpay    │  User A      │  TTL expires
    │  webhook     │  cancels     │  (SettlementExpiryJob)
    │              │              │
    v              v              v
┌──────────┐ ┌──────────┐ ┌──────────┐
│CONFIRMED │ │CANCELLED │ │ EXPIRED  │
└──────────┘ └──────────┘ └──────────┘

    │
    │  Amount mismatch     Payment failed
    │  on webhook          on webhook
    v                      v
┌──────────┐         ┌──────────┐
│  REVIEW  │         │  FAILED  │
└──────────┘         └──────────┘
```

## Detailed Steps

### Step 1: Initiate Settlement

```http
POST /api/settlements/initiate
Content-Type: application/json
Authorization: Bearer <jwt>

{
  "groupId": "group-uuid",
  "payeeId": "user-b-id",
  "amountPaise": 150000
}
```

**System actions:**
1. Validate payer and payee are both members of the group
2. Lazy-expire any stale pending settlements (via `SettlementExpiryHelper`)
3. Create a Razorpay order for the amount
4. Write `Settlement` record with status `Pending` and `ExpiresAt` timestamp
5. Write `SettlementProposed` event to outbox
6. Return Razorpay order details to the client

**Response:**
```json
{
  "settlementId": "settlement-uuid",
  "razorpayOrderId": "order_xxx",
  "amountPaise": 150000,
  "status": "Pending"
}
```

### Step 2: Client-Side Payment

The client uses the Razorpay SDK to complete the payment flow. On success, Razorpay sends a webhook to the backend.

### Step 3: Webhook Confirmation

```http
POST /api/webhooks/razorpay
X-Razorpay-Signature: <hmac-sha256-signature>

{ /* Razorpay webhook payload */ }
```

**System actions:**
1. Verify HMAC-SHA256 signature using webhook secret
2. Optionally validate source IP against whitelist
3. Acquire Valkey distributed lock: `settlement:{settlementId}`
   - Lock prevents concurrent webhook processing for the same settlement
4. Load settlement, validate status is `Pending`
5. Process based on event type:
   - **`payment.captured`**:
     - Amount matches → status = `Confirmed`, set `ConfirmedAt` and `RazorpayPaymentId`
     - Amount mismatch → status = `Review` (requires manual intervention)
   - **`payment.failed`** → status = `Failed`
6. Write corresponding outbox event (`SettlementConfirmed`, `SettlementFailed`)
7. Release distributed lock

### Step 4: Real-time Notification

The `SignalRDispatcherConsumer` picks up the event from Kafka and pushes it to the group's SignalR channel. Both payer and payee see the status update in real-time.

### Step 5: Balance Update

The `DebtSimplifierConsumer` recomputes the optimised debt graph. The `BalanceCalculator` includes confirmed settlements in its net balance computation, so balance queries immediately reflect the settled amount.

## Cancellation

```http
POST /api/settlements/{id}/cancel
Authorization: Bearer <jwt>
```

Only the payer can cancel, and only while status is `Pending`. Writes a `SettlementCancelled` outbox event.

## Expiration

Settlements have an `ExpiresAt` timestamp. Two mechanisms handle expiration:

1. **Lazy expiration**: The `SettlementExpiryHelper` is called on read paths (e.g., when initiating a new settlement). It transitions stale settlements to `Expired` and creates outbox events.
2. **Background job**: The `SettlementExpiryJob` runs every hour, scanning for expired pending settlements and transitioning them.

## Race Condition Prevention

The Valkey distributed lock prevents these scenarios:

| Scenario | Without Lock | With Lock |
|----------|-------------|-----------|
| Duplicate webhook delivery | Two `SettlementConfirmed` events | Second attempt skipped (lock held) |
| Cancel while webhook processes | Inconsistent state | One wins, other blocked |
| Concurrent webhook retries | Double settlement confirmation | Lock serialises processing |

## Idempotency

Settlement initiation supports the `X-Idempotency-Key` header via the `IdempotencyMiddleware`. The response is cached in Valkey with a 24-hour TTL, preventing duplicate Razorpay orders from retried requests.

## Configuration

```json
{
  "Settlement": {
    "ExpiryHours": 48,
    "LockTimeoutSeconds": 30,
    "IdempotencyTtlHours": 24
  },
  "Razorpay": {
    "WebhookSecret": "...",
    "AllowedWebhookIps": []
  }
}
```
