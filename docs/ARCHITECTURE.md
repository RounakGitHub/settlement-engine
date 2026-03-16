# System Architecture

## Design Philosophy

Every financial action is treated as an **immutable event**, never a mutation. You never update a balance — you append an event that changes it. This gives the system auditability, replayability, and correctness. All monetary amounts are stored as **integer paise** (1/100th of a rupee) to eliminate floating-point rounding errors.

## High-Level Architecture

```
                              ┌─────────────────────────────────────────────────────┐
                              │                    WRITE PATH                        │
                              │                                                     │
  ┌──────────┐   HTTP POST    │  ┌────────────┐    ┌───────────┐    ┌────────────┐ │
  │  Client   │──────────────>│  │  Command   │───>│ Idempotency│───>│ PostgreSQL │ │
  │           │               │  │  Handler   │    │  Check     │    │ (Tables +  │ │
  │           │               │  │ (Mediator) │    │  (Valkey)  │    │  Outbox)   │ │
  └──────────┘               │  └────────────┘    └───────────┘    └─────┬──────┘ │
       │                      │                                          │         │
       │                      └──────────────────────────────────────────┼─────────┘
       │                                                                 │
       │                      ┌──────────────────────────────────────────┼─────────┐
       │                      │            EVENT BUS (Kafka KRaft)       │         │
       │                      │                                          v         │
       │                      │  ┌──────────────┐              ┌──────────────┐   │
       │                      │  │expense-events │              │   Outbox      │   │
       │                      │  │settlement-*   │<─────────────│  Publisher    │   │
       │                      │  │group-events   │              │ (Channel +   │   │
       │                      │  │debt-graph-*   │              │  Sweep)      │   │
       │                      │  └──────┬───────┘              └──────────────┘   │
       │                      │         │                                          │
       │                      └─────────┼──────────────────────────────────────────┘
       │                                │
       │        ┌───────────────────────┼───────────────────────┐
       │        │                       │                       │
       │        v                       v                       v
       │  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
       │  │    Debt       │    │   SignalR     │    │     Email        │
       │  │  Simplifier   │    │  Dispatcher   │    │   Notification   │
       │  │              │    │              │    │    Consumer       │
       │  │ Min-edge     │    │ Push events  │    │                  │
       │  │ reduction    │    │ to clients   │    │  SMTP delivery   │
       │  └──────┬───────┘    └──────────────┘    └──────────────────┘
       │         │
       │         v
       │  ┌──────────────┐
       │  │ debt-graph-  │
       │  │ events       │
       │  │ (Kafka)      │
       │  └──────────────┘
       │
       │  ┌──────────────────────────────────────────────────────┐
       │  │                   READ PATH                          │
       │  │                                                      │
       │  │  ┌─────────────┐   GET /balances    ┌──────────┐   │
       │  │  │  Query API  │<──────────────────│  Client   │   │
       │  │  │  (Mediator) │  Computes balances │          │   │
       │  │  │             │  on-the-fly from   └──────────┘   │
       │  │  │             │  PostgreSQL tables                 │
       │  │  └─────────────┘                                    │
       │  └──────────────────────────────────────────────────────┘
       │
       │  SignalR (WebSocket) — Valkey backplane
       │  ┌──────────────┐
       └─>│  Real-time   │  Push: balance updates, settlement notifications,
          │  GroupHub     │  expense changes, member events
          └──────────────┘
```

## Core Flows

### 1. Adding an Expense

```
Client POST /api/groups/{groupId}/expenses
       │
       v
┌─────────────────────────────────────────────────┐
│ AddExpenseCommandHandler (Custom Mediator)        │
│                                                  │
│  1. ValidationBehaviour runs FluentValidation    │
│  2. AuthorisationBehaviour checks group membership│
│  3. Begin EF Core transaction                    │
│     ├── Write Expense + ExpenseSplits to tables  │
│     ├── Write StoredEvent (ExpenseAdded)         │
│     └── Write OutboxEvent (for Kafka relay)      │
│  4. Commit transaction (atomic via PostgreSQL)   │
│                                                  │
│  5. OutboxInterceptor signals OutboxChannel      │
│  6. Return 201 Created                           │
└─────────────────────────────────────────────────┘
       │
       v (near-instant, via OutboxChannel signal)
┌─────────────────────────────────────────────────┐
│ OutboxPublisherService (Hosted Service)           │
│  1. Awaits OutboxChannel signal (or startup sweep)│
│  2. Load unpublished OutboxEvents from PostgreSQL │
│  3. Publish to Kafka topic: expense-events       │
│  4. Mark OutboxEvent as published (set timestamp)│
│  5. If crash before step 4 → startup sweep       │
│     re-publishes (safe: consumers are idempotent)│
└─────────────────────────────────────────────────┘
```

### 2. Debt Simplification

```
Kafka: expense-events (ExpenseAdded / ExpenseEdited / ExpenseDeleted)
       │
       v
┌─────────────────────────────────────────────────┐
│ DebtSimplifierConsumer (Hosted Service)           │
│                                                  │
│  1. Compute net balances for the group           │
│     (BalanceCalculator: credits - debits -       │
│      confirmed settlements)                      │
│  2. Run greedy min-edge reduction algorithm:     │
│     ├── Separate into creditors (+) / debtors (-)│
│     ├── Sort by absolute value descending        │
│     ├── Greedily match largest debtor <> creditor│
│     └── Produces minimum transfer set            │
│  3. Publish DebtGraphUpdated event to Kafka      │
│     (via outbox for atomicity)                   │
└─────────────────────────────────────────────────┘
```

### 3. Settlement Flow (Razorpay-Integrated)

```
User A: POST /api/settlements/initiate
       │
       v
┌─────────────────────────────────────────────────┐
│ Phase 1: Initiate                                │
│  1. Validate payer/payee are group members       │
│  2. Lazy-expire any stale pending settlements    │
│  3. Create Razorpay order (amount in paise)      │
│  4. Write Settlement record (status: Pending)    │
│  5. Write SettlementProposed outbox event        │
│  6. Return Razorpay order details to client      │
│  7. Client completes payment via Razorpay UI     │
└─────────────────────────────────────────────────┘
       │
Razorpay: POST /api/webhooks/razorpay
       │
       v
┌─────────────────────────────────────────────────┐
│ Phase 2: Webhook Confirmation                    │
│  1. Verify HMAC-SHA256 signature                 │
│  2. Acquire Valkey distributed lock:             │
│     settlement:{settlementId}                    │
│     ├── FAIL → skip (concurrent processing)     │
│     └── OK   → continue                         │
│                                                  │
│  3. Load settlement, validate status = Pending   │
│  4. payment.captured:                            │
│     ├── Amount matches → status = Confirmed      │
│     └── Amount mismatch → status = Review        │
│  5. payment.failed → status = Failed             │
│  6. Write SettlementConfirmed/Failed outbox event│
│  7. Release lock                                 │
│  8. SignalR push to both users via Kafka consumer│
└─────────────────────────────────────────────────┘
```

## Data Models (PostgreSQL)

### Users

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | User identifier |
| email | text | Unique email |
| name | text | Display name |
| password_hash | text | BCrypt hash |
| failed_login_attempts | int | Account lockout counter |
| locked_until | timestamp | Lockout expiry |
| created_at | timestamp | Registration time |

### Groups

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Group identifier |
| name | text | Group name |
| currency | text | Default "INR" |
| category | text | Group category |
| created_by | UUID (FK) | Creator user |
| invite_code | text | 8-char alphanumeric join code |
| is_archived | bool | Archive flag |
| deleted_after | timestamp | Auto-cleanup date |

### Expenses

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Expense identifier |
| group_id | UUID (FK) | Parent group |
| paid_by | UUID (FK) | Payer user |
| amount_paise | bigint | Amount in paise (integer arithmetic) |
| description | text | Expense description |
| split_type | enum | Equal / Exact / Percentage |
| deleted_at | timestamp | Soft delete marker |

### Expense Splits

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Split identifier |
| expense_id | UUID (FK) | Parent expense |
| user_id | UUID (FK) | Debtor user |
| amount_paise | bigint | Individual share in paise |

### Settlements

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Settlement identifier |
| group_id | UUID (FK) | Parent group |
| payer_id | UUID (FK) | Who pays |
| payee_id | UUID (FK) | Who receives |
| amount_paise | bigint | Amount in paise |
| status | enum | Pending / Confirmed / Failed / Expired / Cancelled / Review |
| razorpay_order_id | text | Razorpay order reference |
| razorpay_payment_id | text | Razorpay payment reference |
| confirmed_at | timestamp | Confirmation time |
| expires_at | timestamp | Auto-expiry deadline |

### Outbox Events (Transactional Outbox)

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Event identifier |
| event_type | text | Event type name |
| payload | jsonb | Serialised event data |
| published_at | timestamp | When published to Kafka (null = pending) |
| created_at | timestamp | When written |

### Stored Events (Event Log)

| Column | Type | Description |
|--------|------|-------------|
| id | UUID (PK) | Event identifier |
| aggregate_id | UUID | Entity the event belongs to |
| aggregate_type | text | Entity type name |
| event_type | text | Event type name |
| payload | jsonb | Event data |
| version | int | Aggregate version |
| created_at | timestamp | Event time |

## Authentication & Security

- **JWT access tokens** (RSA-2048 signed, 15-minute expiry)
- **Refresh tokens** in HTTP-only, Secure, SameSite=Strict cookies
- **Refresh token rotation** with token family tracking (detects reuse)
- **BCrypt** password hashing (cost factor 12) with legacy PBKDF2 migration support
- **Account lockout** after 10 failed attempts (30-minute window)
- **Rate limiting** (sliding window) per endpoint category
- **Razorpay webhook verification** via HMAC-SHA256 + IP whitelist

## Consistency Model

| Aspect | Guarantee |
|--------|-----------|
| Event writes | **Strong** — PostgreSQL transactions ensure entity + outbox atomicity |
| Kafka publish | **At-least-once** — outbox publisher retries; consumers must be idempotent |
| Debt graph | **Eventually consistent** — may be one event behind, presented as "recommended as of now" |
| Settlement locking | **Linearizable** within a settlement — Valkey distributed lock prevents double-confirm |
| Balance queries | **Strong** — computed on-the-fly from PostgreSQL tables |

## Background Services

| Service | Type | Schedule |
|---------|------|----------|
| OutboxPublisherService | Hosted Service | Channel-driven (near-instant) + startup sweep |
| DebtSimplifierConsumer | Kafka Consumer | Event-driven (expense events) |
| SignalRDispatcherConsumer | Kafka Consumer | Event-driven (all topics) |
| EmailNotificationConsumer | Kafka Consumer | Event-driven (user-relevant events) |
| SettlementExpiryJob | Hosted Service | Every 1 hour |
| GroupArchiveCleanupJob | Hosted Service | Every 24 hours |

## Scalability Design

### Kafka Decouples Throughput from Processing

The command API accepts expense submissions at any rate — events land in the outbox immediately. The outbox publisher, debt simplifier, SignalR dispatcher, and email consumer each process at their own pace. Scale each consumer independently.

### On-the-fly Balance Computation

Balances are computed directly from PostgreSQL tables (expenses, splits, settlements) via the BalanceCalculator. No denormalised read model to keep in sync — queries are always consistent with the latest committed state.

### Scoped Distributed Locks

Settlement locks are keyed as `settlement:{settlementId}`. Two groups settling simultaneously never contend. Horizontal scalability by design.

### Event Log as Scalability Backstop

Immutable stored events in PostgreSQL allow replaying history, rebuilding state, adding new consumers, or auditing without touching production data.
