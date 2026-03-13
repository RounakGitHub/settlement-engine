# Kafka Topics

## Topic Overview

| Topic | Partitioning Key | Producer | Consumers |
|-------|-----------------|----------|-----------|
| `expense-added` | `groupId` | Outbox Worker | Debt Simplifier, Projection Worker |
| `settlement-proposed` | `groupId` | Outbox Worker | Settlement Processor, Projection Worker |
| `settlement-confirmed` | `groupId` | Settlement Processor | Projection Worker |
| `debt-graph-updated` | `groupId` | Debt Simplifier | Projection Worker |
| `outbox-relay` | `eventId` | Outbox Worker | Internal routing |

## Consumer Groups

| Consumer Group | Topics Consumed | Offset Strategy |
|---------------|----------------|-----------------|
| `debt-simplifier-group` | `expense-added` | Manual commit after graph update |
| `projection-worker-group` | `expense-added`, `settlement-confirmed`, `debt-graph-updated` | Manual commit after read model update |
| `settlement-processor-group` | `settlement-proposed` | Manual commit after lock + confirm |

## Partitioning Strategy

All topics are partitioned by `groupId`. This ensures:

- All events for a single group are processed in order within a partition
- Different groups can be processed in parallel across partitions
- Consumer scaling is bounded by partition count, not group count

## Schema Evolution

Events use a versioned schema with a `version` field in metadata. Consumers must handle:

- **Forward compatibility**: Ignore unknown fields in newer event versions
- **Backward compatibility**: Provide defaults for missing fields from older events

## Retention & Compaction

| Topic | Retention | Compaction |
|-------|-----------|------------|
| `expense-added` | 30 days | None (append-only) |
| `settlement-proposed` | 7 days | None |
| `settlement-confirmed` | 30 days | None |
| `debt-graph-updated` | 7 days | Log-compacted (latest per group) |
| `outbox-relay` | 24 hours | None |
