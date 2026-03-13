# System Architecture

## Design Philosophy

Every financial action is treated as an **immutable event**, never a mutation. You never update a balance — you append an event that changes it. This is **event sourcing**, and it's what makes the system auditable, replayable, and correct.

## High-Level Architecture

```
                              ┌─────────────────────────────────────────────────────┐
                              │                    WRITE PATH                        │
                              │                                                     │
  ┌──────────┐   HTTP POST    │  ┌────────────┐    ┌───────────┐    ┌────────────┐ │
  │  Client   │──────────────▶│  │  Command   │───▶│ Idempotency│───▶│  MongoDB   │ │
  │           │               │  │  Handler   │    │  Check     │    │ Event Store│ │
  │           │               │  │ (MediatR)  │    │  (Redis)   │    │ + Outbox   │ │
  └──────────┘               │  └────────────┘    └───────────┘    └─────┬──────┘ │
       │                      │                                          │         │
       │                      └──────────────────────────────────────────┼─────────┘
       │                                                                 │
       │                      ┌──────────────────────────────────────────┼─────────┐
       │                      │              EVENT BUS (Kafka)           │         │
       │                      │                                          ▼         │
       │                      │  ┌──────────────┐              ┌──────────────┐   │
       │                      │  │ expense-added │              │ Outbox Worker│   │
       │                      │  │ settlement-* │◀─────────────│ (Relay)      │   │
       │                      │  │ debt-graph-* │              └──────────────┘   │
       │                      │  └──────┬───────┘                                 │
       │                      │         │                                          │
       │                      └─────────┼──────────────────────────────────────────┘
       │                                │
       │        ┌───────────────────────┼───────────────────────┐
       │        │                       │                       │
       │        ▼                       ▼                       ▼
       │  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
       │  │    Debt       │    │  Projection  │    │   Settlement     │
       │  │  Simplifier   │    │   Worker     │    │   Processor      │
       │  │              │    │              │    │                  │
       │  │ Min-edge     │    │ Denormalised │    │ Lock → Confirm  │
       │  │ reduction    │    │ read model   │    │ → Publish       │
       │  └──────┬───────┘    └──────┬───────┘    └──────────────────┘
       │         │                   │
       │         ▼                   ▼
       │  ┌──────────────┐    ┌──────────────┐
       │  │ debt-graph-  │    │   MongoDB    │
       │  │ updated      │    │  Read Model  │
       │  │ (Kafka)      │    │ (Balances)   │
       │  └──────────────┘    └──────┬───────┘
       │                             │
       │  ┌──────────────────────────┘
       │  │        READ PATH
       │  ▼
       │  ┌──────────────┐   GET /balances    ┌──────────┐
       │  │   Query API  │◀──────────────────│  Client   │
       │  │ (MediatR)    │                    │          │
       │  └──────────────┘                    └──────────┘
       │
       │  SignalR (WebSocket)
       │  ┌──────────────┐
       └─▶│  Real-time   │  Push: balance updates, settlement notifications
          │  Hub          │
          └──────────────┘
```

## Core Flows

### 1. Adding an Expense

```
Client POST /api/expenses
       │
       ▼
┌─────────────────────────────────────────────────┐
│ ExpenseCommand Handler (MediatR)                 │
│                                                  │
│  1. Check idempotency key in Redis               │
│     ├── HIT  → return cached result (no-op)     │
│     └── MISS → continue                         │
│                                                  │
│  2. Begin MongoDB transaction                    │
│     ├── Write ExpenseAdded event to event store  │
│     └── Write event to outbox collection         │
│  3. Commit transaction (atomic)                  │
│                                                  │
│  4. Set idempotency key in Redis (TTL: 24h)     │
│  5. Return 201 Created                           │
└─────────────────────────────────────────────────┘
       │
       ▼ (async, via outbox worker)
┌─────────────────────────────────────────────────┐
│ Outbox Worker                                    │
│  1. Poll outbox collection for unpublished events│
│  2. Publish to Kafka topic: expense-added        │
│  3. Mark outbox entry as published               │
│  4. If crash before step 3 → re-publishes (safe │
│     because consumers are idempotent)            │
└─────────────────────────────────────────────────┘
```

### 2. Debt Simplification

```
Kafka: expense-added
       │
       ▼
┌─────────────────────────────────────────────────┐
│ Debt Simplifier Consumer                         │
│                                                  │
│  1. Load current debt graph for group            │
│  2. Add new expense edges                        │
│  3. Run minimum-edge reduction algorithm:        │
│     ├── Calculate net balance per member          │
│     ├── Separate into creditors (+) / debtors (-)│
│     ├── Greedily match largest debtor ↔ creditor │
│     └── Produces minimum transfer set            │
│  4. Publish DebtGraphUpdated event to Kafka      │
└─────────────────────────────────────────────────┘
```

**Algorithm detail**: The debt simplification is a flow problem on a directed weighted graph. For each group member, compute their net balance (sum of credits minus sum of debits). Then greedily match the largest debtor with the largest creditor, creating a single transfer that partially or fully settles both. Repeat until all balances are zero. This reduces N*(N-1)/2 potential edges to at most N-1 transfers, and often far fewer.

### 3. Settlement Flow

```
User A: POST /api/settlements/propose
       │
       ▼
┌─────────────────────────────────────────────────┐
│ Phase 1: Propose                                 │
│  1. Write SettlementProposed event               │
│  2. Push notification to User B via SignalR      │
└─────────────────────────────────────────────────┘
       │
User B: POST /api/settlements/{id}/confirm
       │
       ▼
┌─────────────────────────────────────────────────┐
│ Phase 2: Confirm                                 │
│  1. Acquire Redis distributed lock:              │
│     settlement:{groupId}:{settlementId}          │
│     ├── FAIL → return 409 Conflict              │
│     └── OK   → continue                         │
│                                                  │
│  2. Validate settlement is still in PROPOSED     │
│  3. Write SettlementConfirmed event              │
│  4. Publish to Kafka                             │
│  5. Release lock                                 │
│  6. Push live update to both users via SignalR   │
└─────────────────────────────────────────────────┘
```

## Data Models

### Event Store (MongoDB: `events` collection)

```json
{
  "_id": "ObjectId",
  "eventId": "UUID",
  "eventType": "ExpenseAdded | SettlementProposed | SettlementConfirmed | DebtGraphUpdated",
  "groupId": "UUID",
  "payload": { /* event-specific data */ },
  "metadata": {
    "actorId": "UUID",
    "timestamp": "ISODate",
    "version": 1,
    "idempotencyKey": "string"
  }
}
```

### Outbox (MongoDB: `outbox` collection)

```json
{
  "_id": "ObjectId",
  "eventId": "UUID",
  "topic": "expense-added",
  "payload": { /* serialised event */ },
  "createdAt": "ISODate",
  "publishedAt": "ISODate | null",
  "retryCount": 0
}
```

### Read Model (MongoDB: `balances` collection)

```json
{
  "_id": "ObjectId",
  "groupId": "UUID",
  "memberId": "UUID",
  "netBalance": -1500.00,
  "owes": [
    { "to": "UUID", "amount": 750.00 },
    { "to": "UUID", "amount": 750.00 }
  ],
  "lastUpdatedEventId": "UUID",
  "updatedAt": "ISODate"
}
```

## Consistency Model

| Aspect | Guarantee |
|--------|-----------|
| Event writes | **Strong** — MongoDB transactions ensure event + outbox atomicity |
| Kafka publish | **At-least-once** — outbox worker retries; consumers must be idempotent |
| Debt graph | **Eventually consistent** — may be one event behind, presented as "recommended as of now" |
| Settlement locking | **Linearizable** within a settlement — Redis distributed lock prevents double-confirm |
| Read model | **Eventually consistent** — projection worker updates asynchronously |

## Scalability Design

### Kafka Decouples Throughput from Processing

The command API accepts expense submissions at any rate — they land in Kafka immediately. The debt simplifier, outbox processor, and projection worker each consume at their own pace. Scale each consumer independently.

### Read/Write Split

Reads vastly outnumber writes. The query API reads from a denormalised read model that never touches the event store. Add read replicas, cache aggressively, or move the read model to a faster store — without touching the write path.

### Scoped Distributed Locks

Settlement locks are keyed as `settlement:{groupId}:{settlementId}`. Two groups settling simultaneously never contend. Horizontal scalability by design.

### Event Log as Scalability Backstop

Immutable events in MongoDB (with TTL indexes) allow replaying history, rebuilding projections, adding new consumers, or migrating read models without touching production data.
