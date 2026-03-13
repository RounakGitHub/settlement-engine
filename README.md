# Settlement Engine

A **distributed split-payment and settlement engine** built on **.NET 10 LTS, Kafka, MongoDB, Redis, and SignalR** — implements debt graph minimisation with eventual consistency, outbox-pattern atomic event publishing, and idempotent command processing under concurrent load, with real-time balance updates.

## The Problem

Group money management today is split across two completely separate tools that never talk to each other. Expense trackers (like Splitwise) track who owes whom but can't move money. Payment apps (like GPay/PhonePe) move money but have no concept of a shared ledger. The user is the glue — manually reconciling the two.

This creates three friction points:

| Problem | Description |
|---------|-------------|
| **Stale Balances** | Ledger drifts from reality when payments happen outside the tracker. Reconciliation becomes a conversation, not a system. |
| **Over-Payments** | Naive settlement in a group of N means up to N-1 transfers. But many debts cancel out — A owes B, B owes C, C owes A — and nobody needs to pay. |
| **No Settlement State Machine** | "I'll pay you back" has no confirmation handshake, no audit trail, no "this debt is now closed" event both parties agree on. |

## The Solution

The Settlement Engine makes the **ledger the source of truth** — an append-only event log that both tracks balances AND orchestrates settlements, with real-time visibility for every group member.

## Architecture Overview

```
┌─────────────┐     ┌──────────────────┐     ┌──────────────┐
│   Client     │────▶│  ASP.NET Core    │────▶│   MongoDB    │
│  (SignalR)   │◀────│  Web API + CQRS  │     │ (Event Store │
└─────────────┘     │  (MediatR)       │     │  + Outbox)   │
                    └────────┬─────────┘     └──────┬───────┘
                             │                       │
                    ┌────────▼─────────┐    ┌───────▼────────┐
                    │      Redis       │    │  Outbox Worker  │
                    │ (Idempotency +   │    │  (Relay events  │
                    │  Dist. Locks)    │    │   to Kafka)     │
                    └──────────────────┘    └───────┬────────┘
                                                    │
                                           ┌────────▼────────┐
                                           │   Apache Kafka   │
                                           │                  │
                                           │  Topics:         │
                                           │  • expense-added │
                                           │  • settlement-*  │
                                           │  • debt-graph-*  │
                                           └──┬─────┬─────┬──┘
                                              │     │     │
                              ┌───────────────┘     │     └───────────────┐
                              ▼                     ▼                     ▼
                    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
                    │    Debt      │     │  Projection  │     │  Settlement  │
                    │ Simplifier   │     │   Worker     │     │  Processor   │
                    │  (Graph Min) │     │ (Read Model) │     │ (Confirm/Ack)│
                    └──────────────┘     └──────────────┘     └──────────────┘
```

> See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full system design.

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Language / Runtime | C# / .NET 10 LTS |
| API Framework | ASP.NET Core Web API |
| CQRS Pipeline | MediatR |
| Messaging | Apache Kafka |
| Primary Store | MongoDB (event store + outbox) |
| Cache / Locks | Redis |
| Real-time | SignalR |
| Containers | Docker + Kubernetes |
| CI/CD | Azure DevOps |

## Key Distributed Systems Patterns

- **Event Sourcing** — All state derived from immutable events, never mutated
- **Outbox Pattern** — Atomic write + publish without distributed transactions
- **Idempotency Keys** — Exactly-once command processing via Redis
- **CQRS** — Separate read model (fast queries) from write model (event store)
- **Distributed Locking** — Redis locks scoped per settlement to prevent races
- **Eventual Consistency** — Debt graph updated asynchronously, clearly communicated

## Project Structure

```
settlement-engine/
├── src/
│   ├── SettlementEngine.Api/           # ASP.NET Core Web API (entry point)
│   ├── SettlementEngine.Domain/        # Domain models, events, value objects
│   ├── SettlementEngine.Application/   # Commands, queries, handlers (MediatR)
│   ├── SettlementEngine.Infrastructure/# MongoDB, Kafka, Redis, SignalR impl
│   └── SettlementEngine.Workers/       # Background workers (outbox, projections, debt simplifier)
├── tests/
│   ├── SettlementEngine.UnitTests/
│   ├── SettlementEngine.IntegrationTests/
│   └── SettlementEngine.LoadTests/
├── docs/
│   ├── ARCHITECTURE.md
│   ├── KAFKA_TOPICS.md
│   ├── SETTLEMENT_FLOW.md
│   └── DEBT_SIMPLIFICATION.md
├── docker/
│   ├── docker-compose.yml
│   ├── Dockerfile
│   └── docker-compose.infra.yml
├── k8s/
│   ├── deployment.yaml
│   ├── service.yaml
│   └── configmap.yaml
└── SettlementEngine.sln
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/)

### Run Infrastructure

```bash
docker compose -f docker/docker-compose.infra.yml up -d
```

This starts MongoDB, Redis, Kafka, and Zookeeper locally.

### Run the Application

```bash
dotnet run --project src/SettlementEngine.Api
```

### Run Tests

```bash
dotnet test
```

## Kafka Topics

| Topic | Producer | Consumer | Purpose |
|-------|----------|----------|---------|
| `expense-added` | Outbox Worker | Debt Simplifier, Projection Worker | New expense event |
| `settlement-proposed` | Outbox Worker | Settlement Processor | Settlement initiated |
| `settlement-confirmed` | Settlement Processor | Projection Worker | Settlement completed |
| `debt-graph-updated` | Debt Simplifier | Projection Worker | Optimised debt graph |
| `outbox-relay` | Outbox Worker | Internal | Reliable event relay |

## Observability

The system instruments the following metrics for production readiness:

- **Kafka consumer lag** per topic — detect if debt-simplifier falls behind
- **Settlement confirmation latency** — proposed → confirmed p50/p95
- **Idempotency key hit rate** — proves duplicate protection is firing
- **Outbox relay lag** — time between event write and Kafka publish
- **Redis lock contention rate** per group

## License

MIT
