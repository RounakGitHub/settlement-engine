# Kafka Topics

## Topic Overview

All topics use **category-based grouping** — multiple event types are published to the same topic based on their domain. Events are routed to the correct topic via the `KafkaOptions.Topics` mapping in `appsettings.json`.

| Topic | Events | Partitioning Key | Producer |
|-------|--------|-----------------|----------|
| `expense-events` | ExpenseAdded, ExpenseEdited, ExpenseDeleted | groupId | Outbox Publisher |
| `settlement-events` | SettlementProposed, SettlementConfirmed, SettlementExpired, SettlementCancelled | groupId | Outbox Publisher |
| `group-events` | GroupCreated, MemberJoined, MemberLeft, GroupArchived | groupId | Outbox Publisher |
| `debt-graph-events` | DebtGraphUpdated | groupId | Outbox Publisher |

## Consumer Groups

| Consumer Group | Topics Consumed | Purpose |
|---------------|----------------|---------|
| `debt-simplifier` | `expense-events` | Recomputes optimised debt graph on expense changes |
| `signalr-dispatcher` | All topics | Pushes all events to connected clients via SignalR |
| `email-notifications` | `expense-events`, `settlement-events`, `group-events` | Sends HTML email notifications for user-relevant events |

## Partitioning Strategy

All topics are partitioned by `groupId`. This ensures:

- All events for a single group are processed **in order** within a partition
- Different groups can be processed **in parallel** across partitions
- Consumer scaling is bounded by partition count (currently 3 per topic)

## Event Envelope Format

All events are published as JSON envelopes:

```json
{
  "eventType": "ExpenseAdded",
  "groupId": "uuid",
  "payload": { /* event-specific data */ },
  "timestamp": "ISO 8601"
}
```

## Outbox Publisher

Events are not published directly to Kafka. Instead:

1. Command handlers write an `OutboxEvent` row in the same PostgreSQL transaction as the domain change
2. The `OutboxPublisherService` (hosted service) picks up unpublished events and publishes them to Kafka
3. On successful publish, the outbox row is marked with a `published_at` timestamp

The publisher uses two mechanisms:
- **Channel signal**: The `OutboxInterceptor` fires a signal via `OutboxChannel` on every `SaveChanges`, waking the publisher for near-instant relay
- **Startup sweep**: On application start, the publisher queries for any unpublished events (crash recovery)

This guarantees **at-least-once delivery** — consumers must be idempotent.

## Consumer Implementation

All consumers extend the `KafkaConsumerService<T>` base class which provides:

- Topic subscription from `KafkaOptions` configuration
- Automatic offset management
- Graceful shutdown via `CancellationToken`
- Structured logging with topic and group context

### DebtSimplifierConsumer

Triggers on: `ExpenseAdded`, `ExpenseEdited`, `ExpenseDeleted`

1. Extracts `groupId` from the event
2. Computes net balances via `BalanceCalculator`
3. Runs `DebtSimplifier` greedy algorithm
4. Publishes `DebtGraphUpdated` outbox event

### SignalRDispatcherConsumer

Triggers on: All events across all topics

1. Deserialises event envelope
2. Dispatches to the appropriate SignalR group via `GroupHubDispatcher`
3. Connected clients receive real-time updates

### EmailNotificationConsumer

Triggers on: ExpenseAdded, ExpenseEdited, ExpenseDeleted, SettlementConfirmed, SettlementExpired, MemberJoined, MemberLeft

1. Resolves affected users for the event
2. Renders HTML email from templates
3. Sends via SMTP with per-recipient error handling

## Configuration

Topics and consumer groups are configured in `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "EnableIdempotence": true,
    "Topics": {
      "expense-events": ["ExpenseAdded", "ExpenseEdited", "ExpenseDeleted"],
      "settlement-events": ["SettlementProposed", "SettlementConfirmed", "SettlementExpired", "SettlementCancelled"],
      "group-events": ["GroupCreated", "MemberJoined", "MemberLeft", "GroupArchived"],
      "debt-graph-events": ["DebtGraphUpdated"]
    },
    "Consumers": {
      "DebtSimplifierGroupId": "debt-simplifier",
      "SignalRDispatcherGroupId": "signalr-dispatcher",
      "EmailNotificationGroupId": "email-notifications"
    }
  }
}
```
