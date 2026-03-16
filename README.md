# Splitr

A **distributed split-payment and settlement engine** built on **.NET 10 LTS, Kafka, PostgreSQL, Valkey, and SignalR** — implements debt graph minimisation with eventual consistency, transactional outbox-pattern event publishing, Razorpay-integrated payments, and idempotent command processing under concurrent load, with real-time balance updates.

## The Problem

Group money management today is split across two completely separate tools that never talk to each other. Expense trackers (like Splitwise) track who owes whom but can't move money. Payment apps (like GPay/PhonePe) move money but have no concept of a shared ledger. The user is the glue — manually reconciling the two.

This creates three friction points:

| Problem | Description |
|---------|-------------|
| **Stale Balances** | Ledger drifts from reality when payments happen outside the tracker. Reconciliation becomes a conversation, not a system. |
| **Over-Payments** | Naive settlement in a group of N means up to N-1 transfers. But many debts cancel out — A owes B, B owes C, C owes A — and nobody needs to pay. |
| **No Settlement State Machine** | "I'll pay you back" has no confirmation handshake, no audit trail, no "this debt is now closed" event both parties agree on. |

## The Solution

Splitr makes the **ledger the source of truth** — an append-only event log that tracks balances, orchestrates settlements via Razorpay payment integration, and provides real-time visibility for every group member.

## Architecture Overview

```
┌─────────────┐     ┌──────────────────┐     ┌──────────────┐
│   Client     │────>│  ASP.NET Core    │────>│  PostgreSQL  │
│  (SignalR)   │<────│  Web API + CQRS  │     │ (Relational  │
└─────────────┘     │  (Custom Mediator)│     │  + Outbox)   │
                    └────────┬─────────┘     └──────┬───────┘
                             │                       │
                    ┌────────v─────────┐    ┌───────v────────┐
                    │      Valkey      │    │ Outbox Publisher│
                    │ (Idempotency +   │    │ (Channel-driven │
                    │  Dist. Locks +   │    │  relay to Kafka)│
                    │  SignalR backplane│    └───────┬────────┘
                    └──────────────────┘            │
                                           ┌────────v────────┐
                                           │   Apache Kafka   │
                                           │   (KRaft mode)   │
                                           │                  │
                                           │  Topics:         │
                                           │  - expense-events│
                                           │  - settlement-*  │
                                           │  - group-events  │
                                           │  - debt-graph-*  │
                                           └──┬─────┬─────┬──┘
                                              │     │     │
                              ┌───────────────┘     │     └───────────────┐
                              v                     v                     v
                    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
                    │    Debt      │     │   SignalR     │     │    Email     │
                    │ Simplifier   │     │  Dispatcher   │     │ Notification │
                    │  (Graph Min) │     │ (Real-time)   │     │  Consumer    │
                    └──────────────┘     └──────────────┘     └──────────────┘
```

> See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full system design.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language / Runtime | C# / .NET 10 LTS |
| API Framework | ASP.NET Core Web API |
| CQRS Pipeline | Custom Mediator (reflection-based) |
| Messaging | Apache Kafka (KRaft, no ZooKeeper) |
| Primary Store | PostgreSQL 17 (EF Core + snake_case) |
| Cache / Locks | Valkey 8 (Redis-compatible) |
| Real-time | SignalR (Valkey backplane) |
| Payments | Razorpay (webhook-driven) |
| Email | SMTP (HTML templates) |
| Observability | OpenTelemetry (OTLP exporter) |
| Containers | Docker Compose |

## Key Distributed Systems Patterns

- **Event Sourcing** — All state changes recorded as immutable events in a stored events table
- **Transactional Outbox** — Atomic write + publish without distributed transactions, channel-driven for near-instant relay
- **Idempotency Keys** — Exactly-once command processing via Valkey with TTL
- **CQRS** — Separate command handlers from query handlers via custom mediator
- **Distributed Locking** — Valkey locks scoped per settlement to prevent race conditions
- **Eventual Consistency** — Debt graph updated asynchronously via Kafka consumers

## Project Structure

```
splitr/
├── backend/
│   └── src/
│       ├── Splitr.API/                # ASP.NET Core Web API (entry point)
│       │   ├── Controllers/             # Auth, Groups, Expenses, Settlements
│       │   ├── Hubs/                    # SignalR GroupHub
│       │   ├── Middleware/              # Exception handling, Idempotency
│       │   └── Configuration/           # Rate limiting, auth setup
│       ├── Splitr.Application/        # Commands, queries, handlers, validators
│       │   ├── Commands/                # Register, Login, AddExpense, etc.
│       │   ├── Queries/                 # GetGroupBalances, GetSettlementPlan, etc.
│       │   ├── Handlers/               # Command & query handler implementations
│       │   ├── Mediator/               # Custom mediator + pipeline behaviours
│       │   └── Interfaces/             # Abstractions for infrastructure
│       ├── Splitr.Domain/             # Domain models, enums, algorithms
│       │   ├── Entities/               # User, Group, Expense, Settlement, etc.
│       │   ├── Enums/                  # SplitType, SettlementStatus, GroupRole
│       │   └── Algorithms/             # DebtSimplifier, BalanceCalculator
│       ├── Splitr.Infrastructure/     # PostgreSQL, Kafka, Valkey, SMTP
│       │   ├── Persistence/            # EF Core DbContext, configurations, migrations
│       │   ├── Messaging/              # Kafka producer, consumer base, outbox publisher
│       │   ├── Consumers/              # DebtSimplifier, SignalR dispatcher, Email
│       │   ├── Caching/               # Valkey distributed locks, idempotency
│       │   ├── Services/              # JWT, SMTP, Razorpay, password hashing
│       │   └── Jobs/                  # Settlement expiry, group archive cleanup
│       └── Splitr.Tests/             # Unit and integration tests
├── docs/
│   ├── ARCHITECTURE.md
│   ├── KAFKA_TOPICS.md
│   ├── SETTLEMENT_FLOW.md
│   └── DEBT_SIMPLIFICATION.md
├── docker-compose.yml                   # PostgreSQL, Valkey, Kafka, pgAdmin, Kafka UI
└── infra/                               # Infrastructure configuration
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/)

### Run Infrastructure

```bash
docker compose up -d
```

This starts PostgreSQL, Valkey, Kafka (KRaft mode), pgAdmin, Kafka UI, and RedisInsight locally.

| Service | Port |
|---------|------|
| PostgreSQL | `5432` |
| Valkey | `6379` |
| Kafka | `9092` |
| pgAdmin | `5050` |
| Kafka UI | `8085` |
| RedisInsight | `5540` |

### Run the Application

```bash
cd backend
dotnet run --project src/Splitr.API
```

### Run Tests

```bash
cd backend
dotnet test
```

## API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/register` | Register a new user |
| POST | `/login` | Login (returns JWT + sets refresh cookie) |
| POST | `/refresh` | Refresh access token via HTTP-only cookie |
| POST | `/logout` | Revoke refresh token |

### Groups (`/api/groups`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/` | Create a new group |
| GET | `/join/{code}` | Preview group before joining |
| POST | `/join/{code}` | Join group with invite code |
| POST | `/{id}/leave` | Leave a group |
| POST | `/{id}/regenerate-invite` | Regenerate invite code |
| GET | `/{id}/balances` | Get member balances |
| GET | `/{id}/expenses` | List group expenses |
| GET | `/{id}/settlement-plan` | Get optimised settlement transfers |

### Expenses (`/api/groups/{groupId}/expenses`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/` | Add expense (Equal/Exact/Percentage splits) |
| PUT | `/{expenseId}` | Edit expense |
| DELETE | `/{expenseId}` | Soft-delete expense |

### Settlements (`/api/settlements`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/initiate` | Initiate settlement (creates Razorpay order) |
| POST | `/{id}/cancel` | Cancel pending settlement |
| POST | `/api/webhooks/razorpay` | Razorpay payment webhook |

### Real-time (`/api/hubs/groups`)

SignalR hub broadcasting: ExpenseAdded, ExpenseEdited, ExpenseDeleted, SettlementConfirmed, SettlementProposed, SettlementFailed, BalanceUpdated, MemberJoined, MemberLeft, DebtGraphUpdated.

## Kafka Topics

| Topic | Events | Consumers |
|-------|--------|-----------|
| `expense-events` | ExpenseAdded, ExpenseEdited, ExpenseDeleted | Debt Simplifier, SignalR Dispatcher, Email |
| `settlement-events` | SettlementProposed, SettlementConfirmed, SettlementExpired, SettlementCancelled | SignalR Dispatcher, Email |
| `group-events` | GroupCreated, MemberJoined, MemberLeft, GroupArchived | SignalR Dispatcher, Email |
| `debt-graph-events` | DebtGraphUpdated | SignalR Dispatcher |

## Observability

Instrumented with OpenTelemetry:

- **ASP.NET Core traces** — request pipeline visibility
- **Kafka consumer lag** — detect if consumers fall behind
- **Settlement confirmation latency** — initiated to confirmed timing
- **Idempotency key hit rate** — duplicate protection metrics
- **Outbox relay lag** — time between event write and Kafka publish

## License

MIT
