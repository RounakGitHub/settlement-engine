# SettleUp

Distributed Group Expense & Settlement Engine

**Complete Engineering Reference — v2.0 (Updated Stack)**

.NET 10 • PostgreSQL 17 • Kafka • Valkey • SignalR • Next.js 15 • AWS EKS

# Section 1 — Technology Stack

Every choice made from first principles. No legacy decisions from previous employment carried over.

## 1.1 Full Stack at a Glance

|                      |                                                                |
|----------------------|----------------------------------------------------------------|
| **Runtime**          | .NET 10 (LTS, released Nov 2025)                               |
| **API framework**    | ASP.NET Core Web API 10                                        |
| **CQRS pipeline**    | Custom Mediator (reflection-based, with pipeline behaviours)   |
| **Validation**       | FluentValidation 11.x                                          |
| **ORM**              | Entity Framework Core 10 + Npgsql (PostgreSQL driver)          |
| **Primary database** | PostgreSQL 17 (AWS RDS)                                        |
| **Read model**       | On-the-fly computation via BalanceCalculator (no materialised views) |
| **DB migrations**    | EF Core Migrations (versioned, source-controlled)              |
| **DB naming**        | EFCore.NamingConventions (snake_case in PostgreSQL)            |
| **Messaging**        | Apache Kafka 3.7 (KRaft mode, no ZooKeeper) via Confluent.Kafka |
| **Cache / locks**    | Valkey 8 (Redis-compatible, AWS ElastiCache)                   |
| **Real-time**        | ASP.NET Core SignalR (Valkey backplane)                        |
| **Background jobs**  | Hosted services (IHostedService with Timer-based scheduling)   |
| **Outbox relay**     | Channel-driven OutboxPublisherService (near-instant + startup sweep) |
| **Payment gateway**  | Razorpay (sandbox → production, same code)                     |
| **Email**            | SMTP via SmtpEmailService (HTML templates)                     |
| **Frontend**         | Next.js 15 + TypeScript (App Router, React Server Components)  |
| **UI components**    | shadcn/ui + Tailwind CSS v4                                    |
| **State management** | Zustand (lightweight, no Redux boilerplate)                    |
| **HTTP client**      | Axios + React Query (TanStack Query v5)                        |
| **Auth (frontend)**  | NextAuth.js v5                                                 |
| **Observability**    | OpenTelemetry SDK → Prometheus + Grafana + Jaeger (traces)     |
| **Logging**          | Serilog → structured console output → AWS CloudWatch Logs      |
| **Containerisation** | Docker (multi-stage builds)                                    |
| **Orchestration**    | Kubernetes on AWS EKS 1.31                                     |
| **CI/CD**            | GitHub Actions (OIDC to AWS, no long-lived credentials)        |
| **Secrets**          | AWS Secrets Manager (injected at pod startup)                  |
| **DNS + CDN**        | AWS Route 53 + CloudFront                                      |
| **Load balancer**    | AWS ALB (Application Load Balancer, Layer 7)                   |
| **WAF**              | AWS WAF (DDoS, SQLi, XSS, bot protection at edge)              |

## 1.2 Key Technology Decisions Explained

### Why PostgreSQL over MongoDB

Expenses, settlements, groups, and users are fundamentally relational. A settlement references an expense; an expense references group members; members belong to groups. PostgreSQL enforces referential integrity at the database level — a settlement can never reference a deleted expense because the foreign key constraint rejects it.

Balances are computed on-the-fly by the BalanceCalculator directly from the expenses, splits, and settlements tables. This gives strong consistency on every read — no stale projections, no eventual consistency lag on balance queries. The query is fast because it operates on indexed foreign keys within a single group scope.

The JSONB column type in PostgreSQL stores event payloads as flexible JSON inside relational rows, giving you relational ordering on the outside and schema flexibility on the inside for the outbox and stored events tables. Best of both worlds.

> *All amounts stored as BIGINT in paise (1 INR = 100 paise). Zero floating-point arithmetic in the entire codebase. 100.50 INR is stored as the integer 10050. This is how every production payment system works.*

### Why Custom Mediator over MediatR

The custom mediator implementation uses reflection to wire handlers and pipeline behaviours at startup, providing the same ISender/IRequestHandler/IPipelineBehavior abstractions without a third-party dependency. This gives full control over the dispatch pipeline, including the ValidationBehaviour (FluentValidation integration) and AuthorisationBehaviour (group membership checks via IRequireGroupMembership marker interface). Zero NuGet bloat for a pattern that requires ~100 lines of code.

### Why Hosted Services over Quartz.NET

The background workload is simple: periodic timer-based jobs (settlement expiry every hour, group cleanup every 24 hours) and event-driven Kafka consumers. .NET's built-in IHostedService with Timer handles periodic jobs cleanly without a heavyweight scheduling framework. The outbox publisher uses an in-memory Channel for near-instant event relay — no polling interval needed. Quartz.NET's clustered scheduling, cron expressions, and PostgreSQL-backed job persistence are overkill when you have 2 timer jobs and 3 Kafka consumers.

### Why Valkey over Redis

Valkey is the open-source fork of Redis maintained by the Linux Foundation after Redis Ltd changed its license. It's wire-compatible with Redis, runs the same commands, and works with all existing Redis clients. AWS ElastiCache supports Valkey natively. Choosing Valkey avoids Redis licensing concerns while maintaining identical functionality for distributed locks, idempotency caching, and the SignalR backplane.

### Why Next.js 15 with App Router

Next.js App Router (stable since v13, production-proven in v14/15) gives you React Server Components — expense lists and group details render on the server and arrive as HTML. This means faster first paint, no loading spinner for the initial data fetch, and better Core Web Vitals scores. SignalR client code and interactive components (add expense form, payment flow) run as Client Components in the browser. The architecture matches how production fintech frontends are built in 2025.

### Why GitHub Actions over Azure DevOps

GitHub Actions is the default CI/CD for any new personal or open-source project. It integrates with AWS via OIDC (OpenID Connect), which means no long-lived AWS credentials stored as secrets — GitHub requests a short-lived token per deployment. The workflow files live in the same repository as the code, making the entire build and deploy pipeline auditable. Azure DevOps is appropriate for enterprise work but adds unnecessary complexity here.

### Why OpenTelemetry

OpenTelemetry (OTel) is the CNCF standard for observability instrumentation. Instead of writing Prometheus-specific metrics code, you instrument with the OTel SDK once and export to any backend. .NET 10 ships with built-in OTel support via Activity and Meter APIs. You get distributed traces across services (Jaeger), metrics (Prometheus/Grafana), and logs from one instrumentation pass. The backend is already instrumented with ASP.NET Core tracing and OTLP exporter.

# Section 2 — Scalability

How the system handles growth from 100 users to 10 million users. Every layer designed to scale independently.

## 2.1 Load Balancing

### AWS ALB (Application Load Balancer)

The ALB sits between the internet and your Kubernetes pods. It operates at Layer 7 (HTTP/HTTPS), which means it understands application-level routing — not just raw TCP.

- SSL termination: ALB decrypts HTTPS traffic and forwards plain HTTP inside the VPC. Your pods never handle certificates.

- Path-based routing: /api/\* routes to .NET API pods; /\* routes to Next.js pods. One ALB, multiple services.

- Health checks: ALB pings /health/live on every pod every 30 seconds. Unhealthy pods are removed from rotation within 60 seconds. Traffic never reaches a crashed pod.

- Sticky sessions for SignalR: ALB supports session affinity (cookie-based). A user's SignalR WebSocket connection is pinned to the same pod for its lifetime, avoiding reconnect thrash.

- Connection draining: when a pod is shutting down (scale-in or deploy), ALB stops sending new connections but waits up to 30 seconds for existing connections to close gracefully.

### AWS Route 53 + CloudFront

- Route 53 handles DNS with latency-based routing — Indian users are resolved to the Mumbai region automatically.

- CloudFront CDN caches Next.js static assets (JS, CSS, images) at 3 Indian edge locations (Mumbai, Chennai, Hyderabad). A user in Chennai fetches your app shell from a server 10ms away instead of your origin.

- Next.js ISR (Incremental Static Regeneration) pages are cached at the edge with configurable TTLs. Group landing pages, for example, can be edge-cached for 60 seconds.

## 2.2 Horizontal Scaling — Every Layer

**API pods — Kubernetes HPA**

Kubernetes Horizontal Pod Autoscaler watches CPU and memory on your API deployment. Configuration:

- Minimum replicas: 2 (always 2 pods running for availability)

- Maximum replicas: 20

- Scale-up trigger: CPU \> 70% for 60 seconds

- Scale-down trigger: CPU \< 30% for 5 minutes (cool-down prevents thrash)

- New pod ready in ~45 seconds. At traffic spikes, HPA spins up pods before the queue backs up.

> *For production: add KEDA (Kubernetes Event-Driven Autoscaler) to scale consumer pods based on Kafka consumer lag — not just CPU. If 10,000 expense events are waiting in the topic, KEDA spins up more consumer pods immediately.*

**PostgreSQL — read replicas**

- 1 primary instance handles all writes (INSERT, UPDATE, DELETE, transactions)

- 2 read replicas handle all SELECT queries (balance lookups, expense lists, group details)

- Reads outnumber writes 10:1 in a typical expense app. Replicas absorb that load entirely.

- EF Core configuration: separate DbContext connection strings per operation type. Write operations use primary; query handlers use read replica.

- AWS RDS handles replication automatically with \< 100ms lag. Failover to a replica is automatic if primary goes down (Multi-AZ deployment).

**Valkey — ElastiCache Cluster Mode**

- 3-node Valkey cluster with automatic sharding. Keyspace split across nodes.

- Idempotency keys for User A land on shard 1, User B on shard 2 — no single node bottleneck.

- Each Valkey node handles ~100,000 operations/second. 3 nodes = 300,000 ops/sec theoretical throughput.

- Read replicas per shard for high-availability. Automatic failover on node failure.

**Kafka — partitioned topics (KRaft mode)**

- 3 Kafka brokers (production minimum). KRaft mode — no ZooKeeper dependency. Kafka manages its own metadata via the controller quorum.

- Topics partitioned with groupId as partition key.

- All events for a group are processed in order (same partition). Events for different groups processed in parallel (different partitions).

- Consumer group scaling: add more consumer pods and Kafka redistributes partition assignments automatically. Linear throughput scaling.

- Retention: events retained for 7 days by default. Allows replay and debugging of any incident.

**Database connection pooling — PgBouncer**

PostgreSQL has a hard limit on concurrent connections (~500 on RDS). At 20 API pods each holding 10 connections, you hit 200 connections fast. PgBouncer is a lightweight connection pooler that sits between your pods and PostgreSQL:

- Pods connect to PgBouncer (lightweight, handles thousands of connections)

- PgBouncer maintains a smaller pool of actual PostgreSQL connections

- Transaction-mode pooling: a PostgreSQL connection is only held for the duration of a transaction, then returned to the pool

- Deploy as a sidecar or as a shared deployment. AWS RDS Proxy is the managed alternative.

## 2.3 Scalability at Different User Counts

|            |                        |                                      |                                 |
|------------|------------------------|--------------------------------------|---------------------------------|
| **Users**  | **API pods**           | **PostgreSQL**                       | **Notes**                       |
| 100        | 2 (minimum)            | 1 instance, no replicas              | Free tier / t3.micro            |
| 10,000     | 2–4 (HPA)              | 1 primary + 1 replica                | t3.medium, ~₹3,000/mo           |
| 100,000    | 4–8 (HPA)              | 1 primary + 2 replicas + PgBouncer   | r6g.large, ~₹20,000/mo          |
| 1,000,000  | 8–20 (HPA + KEDA)      | Multi-AZ + read replicas + RDS Proxy | r6g.xlarge, ~₹1,50,000/mo       |
| 10,000,000 | 20+ pods, multi-region | Aurora PostgreSQL (auto-scaling)     | Enterprise tier, custom pricing |

## 2.4 Caching Strategy

- L1 — In-process: IMemoryCache for JWT public keys and Razorpay webhook secrets. Invalidated on restart. Sub-microsecond access.

- L2 — Valkey: Idempotency keys cached for 24 hours. Rate limit counters with sliding window TTL. Distributed locks for settlement processing. SignalR backplane for multi-pod message routing.

- L3 — On-the-fly computation: BalanceCalculator computes net balances directly from PostgreSQL tables (expenses, splits, settlements). Always strongly consistent. No stale cache to invalidate.

- L4 — CloudFront: Next.js static assets indefinitely (content-hashed filenames). ISR pages with configurable TTL per route.

> *Balance computation is always live from PostgreSQL — no cache invalidation complexity. The BalanceCalculator queries indexed tables scoped to a single group, keeping response times fast even without caching.*

# Section 3 — Security

Production-grade security at every layer. Not an afterthought — designed in from the start.

## 3.1 SQL Injection — Why You Are Fully Protected

This is structurally prevented by Entity Framework Core's parameterised query generation. Understanding why is important.

When you write a LINQ query in EF Core:

> context.Expenses.Where(e =\> e.GroupId == groupId && e.DeletedAt == null)

EF Core generates:

> SELECT \* FROM expenses WHERE group_id = \$1 AND deleted_at IS NULL

The value of groupId is passed as parameter \$1 — entirely separate from the SQL structure. PostgreSQL's driver treats it as a data value, never as executable SQL. Even if a user sends '; DROP TABLE expenses; -- as their group ID, PostgreSQL receives it as a literal string, finds no matching group, and returns an empty result. The SQL structure is determined at compile time, not at runtime.

> *EF Core makes SQL injection impossible for all standard queries. The only risk is if you use context.Database.ExecuteSqlRaw() with manual string concatenation — which you simply never do. Always use ExecuteSqlInterpolated() or LINQ.*

**What about NoSQL injection?**

Since we use PostgreSQL (not MongoDB), NoSQL injection is entirely off the table. Worth noting for interviews: this was one of the architectural reasons to prefer PostgreSQL.

## 3.2 Authentication & Authorisation Security

**JWT implementation**

- Access tokens: 15-minute expiry. Short TTL limits exposure window if a token is stolen.

- Refresh tokens: 30-day expiry, stored in HttpOnly cookies (Secure, SameSite=Strict). JavaScript cannot read HttpOnly cookies — XSS attacks cannot steal your auth tokens.

- Refresh token rotation: every time a refresh token is used, it is invalidated and a new one issued. Token family tracking via `token_family` column detects reuse chains.

- Refresh token reuse detection: if a token that was already rotated is presented again, ALL refresh tokens for that user are immediately revoked. This detects token theft.

- JWT signing: RS256 (asymmetric, RSA-2048). Private key signs tokens on the server; public key verifies. Leaked public key cannot forge tokens.

- JWT payload: contains only userId, email, and name. No sensitive data in the payload.

- SignalR auth: JWT passed via query string for WebSocket connections (standard pattern since WebSocket headers aren't customisable from browsers).

**Password hashing**

- BCrypt with cost factor 12 (configurable via `AuthOptions.BcryptCost`).

- Legacy PBKDF2 migration support: passwords hashed with the old algorithm are transparently rehashed to BCrypt on successful login.

**Authorisation rules**

- Every API endpoint requires a valid JWT (except /api/auth/\*, /api/groups/join/{code} preview, and /api/webhooks/razorpay)

- Group membership check enforced via `AuthorisationBehaviour` pipeline behaviour: commands/queries implementing `IRequireGroupMembership` are automatically validated before the handler executes

- Expense edit/delete: only the expense creator or group admin

- Settlement initiation: only the user who owes money (the payer side)

- Admin actions (regenerate invite code): only group admin role

- Account lockout: 10 consecutive failed logins locks account for 30 minutes (configurable via `AuthOptions.MaxFailedAttempts` and `AuthOptions.LockoutMinutes`)

> *Authorisation is enforced at the mediator pipeline level via the AuthorisationBehaviour — not scattered across controllers. One pipeline behaviour handles all group membership checks. No endpoint can accidentally skip authorisation.*

## 3.3 Rate Limiting — Two Layers

### Layer 1 — AWS WAF (infrastructure level)

- IP-based rate limiting: no single IP can make more than 2,000 requests in any 5-minute window

- AWS Managed Rules: OWASP Top 10 ruleset applied automatically (SQLi, XSS, path traversal, etc.)

- Bot Control: known bad bots blocked by signature. Scrapers throttled.

- Geographic restrictions: if desired, block all traffic outside India in early launch phase

- DDoS protection: AWS Shield Standard (free) automatically mitigates volumetric attacks

### Layer 2 — ASP.NET Core rate limiting middleware (application level)

.NET 10 built-in Microsoft.AspNetCore.RateLimiting with sliding window policies:

|                                |                   |                         |                          |
|--------------------------------|-------------------|-------------------------|--------------------------|
| **Policy**                     | **Limit**         | **Window**              | **Key**                  |
| auth-login                     | 5 requests        | 900 seconds per IP      | Block brute force        |
| auth-register                  | 3 requests        | 3600 seconds per IP     | Block mass registration  |
| write                          | 30 requests       | 60 seconds per user     | Block spam               |
| settlement                     | 10 requests       | 60 seconds per user     | Block duplicate payments |
| general                        | 200 requests      | 60 seconds per user     | Fair use                 |

Rate limit responses return HTTP 429 with a Retry-After header indicating when the limit resets. Client-side code handles this gracefully with a user-facing message.

> *Rate limit configuration is driven by `RateLimitOptions` in appsettings.json — all values (permit limits and window seconds) are configurable without code changes.*

## 3.4 Payment Security

**Razorpay webhook HMAC verification**

Every incoming Razorpay webhook is verified by the `RazorpayWebhookVerifier` before any processing occurs:

1.  Extract X-Razorpay-Signature header from the request

2.  Compute HMAC-SHA256 of the raw request body using your Razorpay webhook secret

3.  Compare computed signature to the header value using a constant-time comparison (prevents timing attacks)

4.  If mismatch: return HTTP 400 immediately, log a security alert, process nothing

5.  If match: proceed with settlement processing

> *Never use string equality (==) for HMAC comparison. Use CryptographicOperations.FixedTimeEquals() in .NET. String equality short-circuits on the first mismatched character, leaking timing information that can be exploited to forge signatures.*

**Razorpay IP allowlist**

The webhook endpoint additionally checks that the request originates from Razorpay's published IP ranges (configured via `RazorpayOptions.AllowedWebhookIps`). Empty allowlist disables the check for local development. Even a correctly signed request from an unknown IP is rejected in production. This is defence-in-depth — the HMAC check is the primary control, the IP check is a secondary backup.

**Idempotency on webhook processing**

Razorpay retries webhooks up to 5 times over 24 hours if your server returns anything other than HTTP 200. The `ProcessWebhookCommandHandler` is idempotent:

- Before processing, acquire Valkey distributed lock on `settlement:{settlementId}`

- Check settlement status in PostgreSQL

- If status is already CONFIRMED, return HTTP 200 immediately (do nothing, no error)

- If status is PENDING, process and confirm

- Valkey lock TTL: 30 seconds (configurable via `SettlementOptions.LockTimeoutSeconds`). If lock cannot be acquired, the concurrent request is skipped.

## 3.5 Infrastructure Security

**Network isolation — AWS VPC**

- All databases (RDS PostgreSQL, ElastiCache Valkey) live in private subnets — no public internet access

- Pods communicate with databases via private VPC addresses only

- Only the ALB has a public IP. Everything behind it is private.

- Security groups: each service has a dedicated security group allowing only the specific ports from specific sources (e.g. only API pods can connect to PostgreSQL on port 5432)

**Secrets management**

- Zero secrets in code, environment variables, or Kubernetes manifests

- All secrets (DB password, Valkey password, Razorpay key/secret, JWT RSA private key) stored in AWS Secrets Manager

- Pods retrieve secrets at startup via IAM role — no long-lived credentials

- Secret rotation: database passwords rotated every 90 days automatically by Secrets Manager

- GitHub Actions uses OIDC — no AWS credentials stored as GitHub secrets

> *For local development, secrets are stored in appsettings.json (git-ignored) and .env.example provides the template.*

**HTTPS everywhere**

- AWS Certificate Manager issues free TLS certificates for your domain

- ALB terminates SSL — all internet-facing traffic is encrypted

- HSTS header enforced: browsers will only ever connect via HTTPS after the first visit

- Certificate auto-renewal handled by ACM — no manual expiry management

**Input validation**

- FluentValidation runs on every command via the `ValidationBehaviour` pipeline behaviour before the handler executes

- Amount: must be positive integer (paise), max value enforced (prevents overflow)

- Group name: max length enforced, no control characters

- Description: max length enforced, sanitised before storage

- User IDs in payloads: validated against JWT claims — you cannot act as another user by changing an ID in the request body

**CORS policy**

- Strict CORS: only configured origins allowed (default: `http://localhost:3000` for development)

- Allows any header and any method, with credentials support for cookie-based refresh tokens

- No wildcard (\*) origins in production

- Configured via `Cors:AllowedOrigins` in appsettings.json

# Section 4 — Full Logic Flow

Every user action, system reaction, edge case, and failure mode documented end-to-end.

## 4.1 Authentication

**Registration**

1.  POST /api/auth/register — name, email, password

2.  FluentValidation: email format, password min length with complexity rules

3.  Check email uniqueness in PostgreSQL — 409 Conflict if duplicate

4.  BCrypt hash password (cost factor 12, configurable via `AuthOptions.BcryptCost`)

5.  INSERT user row in EF Core transaction. If transaction fails, no partial state.

6.  Issue JWT (RS256, 15 min) + refresh token (30 days, HttpOnly Secure SameSite=Strict cookie)

7.  Refresh token stored as SHA-256 hash in PostgreSQL `refresh_tokens` table with `token_family` for rotation tracking

**Login**

8.  POST /api/auth/login

9.  Rate limiter: 5 attempts per 900s per IP — returns 429 if exceeded

10. Account lockout check: if `locked_until` is in the future, reject immediately

11. BCrypt.Verify — constant-time, immune to timing attacks

12. Legacy PBKDF2 migration: if password was hashed with old algorithm, rehash to BCrypt on success

13. On failure: increment `failed_login_attempts`, generic 401 message. Never reveal if email exists.

14. On success: reset failed attempts, new JWT + rotated refresh token

**Token refresh**

15. POST /api/auth/refresh — reads refresh token from HttpOnly cookie

16. Hash the presented token with SHA-256, look up in DB

17. Verify token exists, is not expired, and is not revoked

18. Reuse detection: if token already rotated (has `replaced_by_token_hash`), revoke ALL tokens in the same `token_family` and return 401 (theft detected)

19. Issue new JWT + new refresh token, mark old refresh token as replaced

### Edge cases — auth

- Concurrent refresh: two tabs both call refresh simultaneously — only one succeeds; the other detects reuse via token family and forces re-login

- Account lockout: 10 consecutive failures locks account for 30 minutes (configurable), all existing sessions remain valid

- Password stored as BCrypt hash — even database breach doesn't expose plaintext passwords

## 4.2 Group Management

**Creating a group**

20. POST /api/groups — name, currency (INR default), category

21. Creator added as admin member in the same transaction

22. 8-character alphanumeric invite code generated (cryptographically random, collision-checked), stored in the group row

23. GroupCreated outbox event written in the same EF Core transaction

24. OutboxPublisherService relays to Kafka via channel signal (near-instant)

**Joining**

25. GET /api/groups/join/{code} — preview: name, member count, currency (no auth required for preview)

26. POST /api/groups/join/{code} — auth required

27. Idempotency check: if already a member, return 200 with current state

28. MemberJoined outbox event written and published to Kafka

29. All existing members notified via SignalR (through SignalRDispatcherConsumer)

30. Email notifications sent to existing members (through EmailNotificationConsumer)

### Edge cases — groups

- Invite code regeneration: admin calls POST /{id}/regenerate-invite; old code immediately invalidated, new code returned

- Admin leaving: must promote another member first; enforced at command handler level

- Group archival: `is_archived = true`, `delete_after` timestamp set. `GroupArchiveCleanupJob` hard-deletes after retention period (runs every 24 hours).

- Max group size: configurable via `GroupOptions.MaxMembers` in appsettings.json

- Duplicate join: idempotent — returns HTTP 200 with current state, no duplicate event written

## 4.3 Expense Management

**Adding an expense**

31. POST /api/groups/{groupId}/expenses with X-Idempotency-Key header (UUID, client-generated)

32. Rate limiter: 30 requests/60s per user (write policy)

33. IdempotencyMiddleware: check Valkey for key — if exists, return cached response (POST requests only)

34. ValidationBehaviour: FluentValidation runs — amount \> 0, members exist in group, split amounts sum to total

35. AuthorisationBehaviour: verify requesting user is a member of the group (via IRequireGroupMembership)

36. EF Core transaction: INSERT expense + INSERT expense_splits + INSERT stored_event (ExpenseAdded) + INSERT outbox_event — atomic

37. OutboxInterceptor fires OutboxChannel signal on SaveChanges

38. OutboxPublisherService publishes ExpenseAdded to Kafka `expense-events` topic (near-instant)

39. DebtSimplifierConsumer: recompute net balances via BalanceCalculator, run greedy reduction, publish DebtGraphUpdated

40. SignalRDispatcherConsumer: push event to all connected group members via GroupHub

41. EmailNotificationConsumer: send HTML email notifications to affected members via SMTP

**Split types and calculation**

- Equal: total_paise / N; remainder distributed penny-by-penny. E.g. 100 paise / 3 = 33, 33, 34.

- Exact: per-member amounts in paise; server validates sum == total

- Percentage: percentages per member; server converts to paise amounts using integer arithmetic

**Editing an expense**

- Only creator or group admin. PUT /api/groups/{groupId}/expenses/{expenseId}

- ExpenseEdited outbox event written

- Full re-computation of debt graph triggered via Kafka

**Deleting an expense**

- Soft delete only: `deleted_at` timestamp set, row never hard-deleted

- ExpenseDeleted outbox event written

- Debt graph recomputed excluding the soft-deleted expense

### Edge cases — expenses

- Double submit: idempotency key in Valkey prevents any duplicate, even with network retry

- Concurrent adds by two members simultaneously: Kafka partition ordering by groupId ensures sequential processing in consumers

- Outbox guarantees at-least-once delivery; consumer idempotency prevents double-processing

- Optimistic UI: expense appears in list immediately. Reverts with toast if server returns error.

## 4.4 Debt Simplification Engine

**Algorithm (minimum-edge reduction)**

42. BalanceCalculator computes net balance per member from PostgreSQL: sum(expense credits) - sum(expense splits) - sum(confirmed settlement payments out) + sum(confirmed settlement payments in)

43. Separate into creditors (positive) and debtors (negative)

44. Sort both by absolute value descending

45. Greedy match: largest debtor to largest creditor, create transfer for min(\|debtor\|, creditor)

46. Reduce both balances, repeat until all zero

47. Result: at most N-1 transfers for N members. Circular debts resolve to zero transfers.

### Edge cases — debt simplification

- Circular debts: correctly resolves (A owes B, B owes C, C owes A → zero transfers)

- Already settled: all balances zero, no plan generated

- Concurrent expense adds: groupId partition key ensures ordered processing per group

- All arithmetic in integer paise: zero floating-point errors possible

- Settlement plan query (GET /settlement-plan) computes on-the-fly from the database for strong consistency

## 4.5 Settlement Flow

**Full Razorpay flow**

48. User opens Settle Up tab — GET /api/groups/{id}/settlement-plan computes on-the-fly via BalanceCalculator + DebtSimplifier

49. User taps Confirm & Pay on a suggested transfer

50. Backend: POST /api/settlements/initiate — validates payer/payee membership, lazy-expires stale pending settlements via SettlementExpiryHelper

51. Backend: POST to Razorpay API — create order with amount_paise, currency INR

52. EF Core transaction: INSERT settlement (status=Pending, razorpay_order_id, expires_at) + INSERT outbox SettlementProposed

53. Razorpay order details returned to frontend; Razorpay checkout SDK opens in browser

54. User completes payment via UPI / card / net banking inside Razorpay checkout

55. Razorpay fires webhook: POST /api/webhooks/razorpay

56. RazorpayWebhookVerifier: verify HMAC-SHA256 signature (CryptographicOperations.FixedTimeEquals)

57. RazorpayWebhookVerifier: verify source IP is in allowed range (if configured)

58. ProcessWebhookCommandHandler: acquire Valkey distributed lock on `settlement:{settlementId}` (configurable TTL)

59. Load settlement, verify status = Pending

60. payment.captured + amount matches: UPDATE status=Confirmed, set confirmed_at and razorpay_payment_id + INSERT outbox SettlementConfirmed

61. payment.captured + amount mismatch: UPDATE status=Review (requires manual intervention)

62. payment.failed: UPDATE status=Failed + INSERT outbox event

63. Release Valkey lock

64. Kafka publishes settlement-events

65. SignalRDispatcherConsumer pushes live update to all group members

66. EmailNotificationConsumer sends confirmation/failure email via SMTP

### Edge cases — settlement

- Payment failed (webhook: payment.failed): status set to Failed, user notified via SignalR, can reinitiate

- Webhook arrives twice: Valkey lock + Pending status check — second call sees non-Pending status, returns 200 immediately

- HMAC mismatch: 400 returned, security alert logged

- Unknown IP: rejected even if HMAC is valid (when allowlist configured)

- 24-hour expiry (configurable via `SettlementOptions.ExpiryHours`): SettlementExpiryJob runs hourly, finds Pending settlements past expires_at, marks Expired, publishes outbox events, notifies both parties

- Lazy expiry: SettlementExpiryHelper also runs on read paths (e.g., when initiating a new settlement) for immediate cleanup

- Amount mismatch: webhook amount != recorded settlement amount — status set to Review, never auto-confirmed

- Cancellation: POST /api/settlements/{id}/cancel — only payer can cancel, only while Pending

## 4.6 Real-Time Events (SignalR)

|                                 |                                  |
|---------------------------------|----------------------------------|
| **Event**                       | **Recipients**                   |
| ExpenseAdded / Edited / Deleted | All group members online         |
| MemberJoined / Left             | All existing group members       |
| SettlementProposed              | All group members                |
| SettlementConfirmed             | All group members                |
| SettlementFailed                | All group members                |
| SettlementExpired               | All group members                |
| BalanceUpdated                  | All group members online         |
| DebtGraphUpdated                | All group members online         |

- SignalR is enhancement only — never the source of truth. Offline users fetch latest state via REST on next app open.

- Multiple devices: all active connections for a user receive the push.

- Reconnect: exponential backoff (1s, 2s, 4s, max 30s). On reconnect, client fetches delta since last-known state.

- SignalR with multiple API pods: Valkey backplane ensures a message published by pod 2 is delivered to connections on pod 1.

- SignalR hub endpoint: /api/hubs/groups — JWT authentication via query string parameter.

- Event routing: SignalRDispatcherConsumer listens to all Kafka topics and dispatches events to the appropriate SignalR group via GroupHubDispatcher.

# Section 5 — Sprint-by-Sprint Build Plan

12 sprints, 3 months. Use Claude Code for scaffolding and boilerplate. Focus your own time on the distributed systems decisions, payment integration, and security implementation.

## Month 1 — Foundation & Core Backend

> **Target: PostgreSQL schema live, auth working, expense CRUD functional with outbox**

### Sprint 1 — Infrastructure scaffold

- GitHub repo, monorepo structure: /backend (.NET solution), /frontend (Next.js), /k8s (manifests), /infra (Terraform)

- Docker Compose (root level): PostgreSQL 17, Valkey 8, Kafka (KRaft mode, no ZooKeeper), pgAdmin, Kafka UI, RedisInsight

- .NET 10 solution: SettleUp.API, SettleUp.Domain, SettleUp.Application, SettleUp.Infrastructure, SettleUp.Tests

- EF Core 10 + Npgsql setup with EFCore.NamingConventions (snake_case), first migration (users, groups, group_members tables)

- Serilog structured logging to console

- GitHub Actions: build + test on every push to main

### Sprint 2 — Auth service

- Users table + RefreshTokens table (migration) with token_family column for rotation tracking

- Register, Login, Refresh, Logout commands + handlers via custom mediator

- BCrypt hashing (cost factor 12, configurable), RS256 JWT (RSA-2048 PEM key)

- HttpOnly Secure SameSite=Strict cookie for refresh tokens

- Refresh token rotation + reuse detection via token family

- SHA-256 token hashing for secure storage (TokenHasher)

- Rate limiting middleware (Microsoft.AspNetCore.RateLimiting, sliding window policies)

- Account lockout (10 attempts, 30 minutes)

- Unit tests for all auth edge cases

### Sprint 3 — Groups, outbox & custom mediator

- Groups, GroupMembers tables (migration) with invite_code, is_archived, delete_after columns

- CreateGroup, JoinGroup, LeaveGroup, RegenerateInviteCode commands

- Custom Mediator implementation: ISender, IRequestHandler, IPipelineBehavior with reflection-based wiring

- ValidationBehaviour (FluentValidation integration) + AuthorisationBehaviour (IRequireGroupMembership)

- OutboxEvent table (migration): id, event_type, payload JSONB, published_at, created_at

- StoredEvent table (migration): id, aggregate_id, aggregate_type, event_type, payload JSONB, version, created_at

- OutboxInterceptor: fires OutboxChannel signal on EF Core SaveChanges

- OutboxPublisherService (IHostedService): channel-driven near-instant relay to Kafka + startup sweep for crash recovery

- Kafka producer setup (Confluent.Kafka, idempotence enabled)

- KafkaOptions: topic-to-event-type mapping in appsettings.json

### Sprint 4 — Expense service + debt simplifier

- Expenses, ExpenseSplits tables (migration, amounts in BIGINT paise)

- AddExpense, EditExpense, DeleteExpense commands with all split type logic (Equal/Exact/Percentage)

- IdempotencyMiddleware: Valkey check on X-Idempotency-Key header for POST requests, caches response with TTL

- KafkaConsumerService base class (IHostedService, abstract Topics/ConsumerGroupId, graceful shutdown)

- DebtSimplifierConsumer: listens to expense-events, recomputes via BalanceCalculator + DebtSimplifier, publishes DebtGraphUpdated

- ExceptionHandlingMiddleware: ValidationException → 400, UnauthorizedAccessException → 401, Problem+JSON format

- Integration test: add expenses across members, assert minimum transfer count

## Month 2 — Real-time, Payments & Frontend

> **Target: SignalR live, Razorpay sandbox integrated, full settlement flow, frontend auth + groups**

### Sprint 5 — SignalR + Kafka consumers

- SignalR GroupHub setup at /api/hubs/groups with JWT auth via query string

- Valkey backplane for multi-pod SignalR

- SignalRDispatcherConsumer: listens to all Kafka topics, dispatches events to SignalR groups via GroupHubDispatcher

- EmailNotificationConsumer: listens to user-relevant events, sends HTML emails via SMTP (SmtpEmailService)

- GetGroupBalancesQuery: on-the-fly balance computation via BalanceCalculator

- GetSettlementPlanQuery: on-the-fly debt simplification via DebtSimplifier

- Test: two browser tabs, add expense in one, other updates in real-time

### Sprint 6 — Razorpay integration

- POST /api/settlements/initiate: create Razorpay order, write Settlement (status=Pending), SettlementProposed outbox event

- POST /api/webhooks/razorpay: RazorpayWebhookVerifier (HMAC-SHA256 + IP allowlist)

- ProcessWebhookCommandHandler: Valkey distributed lock, status validation, amount verification

- Settlement state machine: Pending → Confirmed / Failed / Expired / Cancelled / Review

- SettlementExpiryHelper: lazy expiration on read paths

- SettlementExpiryJob (IHostedService, hourly Timer): expire Pending settlements past expires_at

- GroupArchiveCleanupJob (IHostedService, daily Timer): hard-delete archived groups past delete_after

- ValkeyDistributedLockService: LOCK commands with configurable TTL

### Sprint 7 — Next.js scaffold + auth screens

- Next.js 15 project: TypeScript, App Router, Tailwind CSS v4, shadcn/ui init

- NextAuth.js v5 configured with credentials provider (calls .NET auth API)

- Zustand store for client-side state (current group, user profile)

- TanStack Query v5 for server state (expenses, balances, settlement plan)

- Sign In and Sign Up pages: React Hook Form + Zod validation

- Protected route middleware (Next.js middleware.ts)

### Sprint 8 — Groups + expenses UI

- Dashboard: groups list, net balance summary card, skeleton loaders

- Create group bottom sheet (Sheet component from shadcn/ui)

- Group detail page: tabbed layout (Expenses / Balances / Settle Up)

- Add expense sheet: amount input (paise conversion), split type toggle, member picker

- SignalR client hook: @microsoft/signalr, reconnect logic, refetch on reconnect

- Optimistic UI on expense submission: appears immediately, reverts on error

## Month 3 — Settlement UI, Observability & Production

> **Target: full end-to-end demo, deployed on AWS EKS, OTel instrumented, load tested**

### Sprint 9 — Settlement UI + payment flow

- Settle Up tab: debt-simplified transfer cards, 'You pay X to Y' layout

- Razorpay checkout: load SDK script, open checkout on button tap

- Payment states: idle → checkout open → confirming (webhook in-flight) → confirmed / failed

- Success screen: canvas-confetti animation on SettlementConfirmed SignalR push

- Settlement history: past confirmed settlements with Razorpay payment IDs

### Sprint 10 — Observability

- OpenTelemetry SDK: ASP.NET Core tracing + metrics + OTLP exporter (already instrumented in backend)

- Custom metrics: settlement confirmation latency, idempotency key hit rate, outbox relay lag, Kafka consumer lag

- Jaeger for distributed traces (see a single expense-add flow traced across API → Kafka → consumer → SignalR)

- Grafana dashboards: one per service, one system-wide overview

- k6 load test: concurrent expense adds + settlement confirms on same group, assert no duplicates

### Sprint 11 — AWS EKS deployment

- Terraform: VPC, subnets, EKS cluster, RDS PostgreSQL (Multi-AZ), ElastiCache Valkey, MSK Kafka (or Confluent Cloud)

- Kubernetes manifests: Deployments, Services, ConfigMaps, Secrets (referencing AWS Secrets Manager via External Secrets Operator)

- AWS ALB Ingress Controller + cert-manager for TLS

- HPA configured for API and worker deployments

- GitHub Actions: build Docker images → push to ECR → kubectl apply via OIDC role

- Smoke test pipeline stage after deploy: hit /health/ready on all services

### Sprint 12 — Polish + demo prep

- Dark mode (Tailwind dark: classes, next-themes for system preference)

- Mobile responsiveness audit at 390px

- Empty states with illustrations

- README: architecture diagram, 'run locally in 3 commands' section, demo walkthrough

- Loom demo recording: create group, add expenses, run settlement, show webhook fires, balances update live

- Update resume with project + one-liner

# Section 6 — UI Specification

Industry-standard, modern, intuitive. Built on Next.js 15 + shadcn/ui + Tailwind CSS v4. Mobile-first.

## 6.1 Design System

**Color tokens**

|                    |                                                     |
|--------------------|-----------------------------------------------------|
| **primary**        | \#1A56DB — buttons, links, active states            |
| **background**     | \#F9FAFB — app background                           |
| **surface**        | \#FFFFFF — cards, sheets, modals                    |
| **border**         | \#E5E7EB — dividers, input borders                  |
| **text-primary**   | \#111827 — headings, important text                 |
| **text-secondary** | \#6B7280 — subtext, labels, timestamps              |
| **success**        | \#059669 — confirmed payments, positive balances    |
| **destructive**    | \#DC2626 — errors, negative balances, failed states |
| **warning**        | \#D97706 — pending states, expiring settlements     |

**Typography & spacing**

- Font: Inter (Google Fonts). Clean, fintech-standard.

- Scale: Display 30px/700, Title 20px/600, Body 14px/400, Caption 12px/400, Mono 13px/400

- Base unit: 4px. Spacing uses multiples: 4, 8, 12, 16, 20, 24, 32, 40, 48px

- Border radius: sm=6px, md=10px, lg=14px, xl=20px, pill=9999px

## 6.2 Screen Specifications

**Sign In / Sign Up**

- Centered card (max-width 400px), white background

- App logo + wordmark at top

- Floating label inputs with inline validation errors

- Password: show/hide toggle. Sign up: strength indicator bar (4 segments)

- Primary full-width button with spinner on submit

**Dashboard**

- Top bar: logo + avatar (taps to profile/logout)

- Net balance card: full-width, shows 'You are owed' or 'You owe', total amount, group breakdown

- Group cards: emoji icon + name (bold) + your net balance (green if owed, red if you owe) + '4 members · 2h ago' caption

- Empty state: illustrated placeholder + 'Create your first group' CTA

**Group detail (tabbed)**

- Header: back arrow + group name + member avatar stack + settings icon

- Balance strip: horizontal scroll of per-member chips

- Tabs: Expenses \| Balances \| Settle Up (underline indicator)

- Expenses tab: date-grouped list, FAB bottom-right for add, each row shows category icon + description + your share

- Balances tab: member cards with net balance + spend progress bar

- Settle Up tab: simplified transfer cards, 'Pay now' CTA on each

**Add Expense sheet**

- Bottom sheet, full height. Amount: XL centered input, INR prefix, numeric keyboard.

- Split toggle: Equal \| Exact \| Percent pills

- Member picker with checkboxes (all selected by default)

- Real-time sum validation ('14 remaining' in red if not balanced)

**Payment flow**

- Confirmation sheet: summary of who pays whom, amount, group

- Razorpay checkout opens on confirm

- 'Confirming your payment...' loading screen while webhook in-flight

- Success: green checkmark + confetti animation on SignalR push

- Failure: red X + retry / cancel buttons

## 6.3 shadcn/ui Components Required

|               |                                                   |
|---------------|---------------------------------------------------|
| **Component** | **Used in**                                       |
| Button        | All CTAs, FAB, tab triggers                       |
| Input         | All text inputs, amount fields                    |
| Card          | Group cards, balance cards, transfer cards        |
| Sheet         | Add expense, create group, payment confirm        |
| Dialog        | Delete confirmations, error modals                |
| Toast         | Success / error feedback after actions            |
| Badge         | Settlement status (Pending / Confirmed / Expired) |
| Avatar        | Member avatars with initials fallback             |
| Tabs          | Group detail navigation                           |
| Skeleton      | All loading states                                |
| Progress      | Member spend share bar                            |
| Popover       | Date picker, info tooltips                        |

# Appendix — Quick Reference

## A1 Kafka Topics & Consumers

|                         |                       |                                             |
|-------------------------|-----------------------|---------------------------------------------|
| **Topic**               | **Consumer group**    | **Purpose**                                 |
| expense-events          | debt-simplifier       | Recompute debt graph on expense changes     |
| expense-events          | signalr-dispatcher    | Push expense events to connected clients    |
| expense-events          | email-notifications   | Email notifications for expense changes     |
| settlement-events       | signalr-dispatcher    | Push settlement events to connected clients |
| settlement-events       | email-notifications   | Email notifications for settlements         |
| group-events            | signalr-dispatcher    | Push group events to connected clients      |
| group-events            | email-notifications   | Email notifications for member changes      |
| debt-graph-events       | signalr-dispatcher    | Push updated debt graph to connected clients|

> *Partition key = groupId for all topics. Guarantees ordering within a group while allowing parallel processing across groups. All topics have 3 partitions.*

## A2 PostgreSQL Schema (Key Tables)

|                    |                                                                                                                                                  |
|--------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| **users**          | id (UUID), email, password_hash, name, failed_login_attempts, locked_until, created_at                                                            |
| **refresh_tokens** | id (UUID), user_id (FK), token_hash (SHA-256), expires_at, revoked_at, token_family (UUID), replaced_by_token_hash, created_at                    |
| **groups**         | id (UUID), name, currency, category, is_archived, archived_at, delete_after, created_by (FK), invite_code (8-char), created_at                    |
| **group_members**  | group_id (FK), user_id (FK), role (Admin/Member enum), joined_at — composite PK                                                                   |
| **expenses**       | id (UUID), group_id (FK), paid_by (FK), amount_paise (BIGINT), description, split_type (Equal/Exact/Percentage), created_at, deleted_at           |
| **expense_splits** | id (UUID), expense_id (FK), user_id (FK), amount_paise (BIGINT), created_at                                                                      |
| **settlements**    | id (UUID), group_id (FK), payer_id (FK), payee_id (FK), amount_paise (BIGINT), status (Pending/Confirmed/Failed/Expired/Cancelled/Review), razorpay_order_id, razorpay_payment_id, confirmed_at, expires_at, created_at |
| **stored_events**  | id (UUID), aggregate_id, aggregate_type, event_type, payload (JSONB), version, created_at                                                         |
| **outbox_events**  | id (UUID), event_type, payload (JSONB), published_at, created_at                                                                                  |

> *All table and column names use snake_case via EFCore.NamingConventions. All monetary amounts are BIGINT paise.*

## A3 Background Services

|                           |                    |                                                    |
|---------------------------|--------------------|----------------------------------------------------|
| **Service**               | **Type**           | **Schedule**                                       |
| OutboxPublisherService    | IHostedService     | Channel-driven (near-instant) + startup sweep      |
| DebtSimplifierConsumer    | Kafka Consumer     | Event-driven (expense-events topic)                |
| SignalRDispatcherConsumer | Kafka Consumer     | Event-driven (all topics)                          |
| EmailNotificationConsumer | Kafka Consumer     | Event-driven (expense, settlement, group events)   |
| SettlementExpiryJob       | IHostedService     | Timer-based, every 1 hour                          |
| GroupArchiveCleanupJob    | IHostedService     | Timer-based, every 24 hours                        |

## A4 Security Checklist

- SQL injection: EF Core parameterised queries. Never ExecuteSqlRaw with string concat.

- XSS: Next.js escapes all output by default. No dangerouslySetInnerHTML.

- CSRF: JWT in HttpOnly cookie with SameSite=Strict. No CSRF token needed for APIs.

- JWT: RS256 (RSA-2048) signing, 15-min expiry, refresh token rotation with reuse detection via token family.

- Password: BCrypt cost factor 12, legacy PBKDF2 migration support.

- Account lockout: 10 failed attempts, 30-minute lockout (configurable via AuthOptions).

- Webhook: HMAC-SHA256 + constant-time comparison + IP allowlist (configurable).

- Rate limiting: AWS WAF (IP level) + ASP.NET Core sliding window middleware (user level).

- Secrets: AWS Secrets Manager in production. Zero secrets in code or env vars.

- Network: all databases in private VPC subnets. Only ALB is public-facing.

- Input validation: FluentValidation on every command via ValidationBehaviour pipeline. Reject at the boundary.

- Authorisation: AuthorisationBehaviour checks group membership via IRequireGroupMembership marker interface.

- HTTPS: ACM certificates, HSTS header enforced.

- CORS: explicit allowed origins only. No wildcard (\*) in production.

## A5 Configuration (appsettings.json)

Key configuration sections with strongly-typed options classes:

|                      |                                                                    |
|----------------------|--------------------------------------------------------------------|
| **AuthOptions**      | BcryptCost, MaxFailedAttempts, LockoutMinutes, RefreshTokenExpiryDays, CookieName, CookiePath |
| **JwtOptions**       | RsaPrivateKeyPem, Issuer, Audience, ExpiryMinutes                  |
| **KafkaOptions**     | BootstrapServers, EnableIdempotence, Topics (event-type mapping), Consumers (group IDs) |
| **RazorpayOptions**  | WebhookSecret, AllowedWebhookIps                                   |
| **SmtpOptions**      | Host, Port, Username, Password, UseSsl, FromAddress                |
| **GroupOptions**     | MaxMembers, ArchiveRetentionDays, InviteCodeLength                 |
| **SettlementOptions**| ExpiryHours, LockTimeoutSeconds, IdempotencyTtlHours              |
| **RateLimitOptions** | Per-policy PermitLimit and WindowSeconds                           |

## A6 Resume One-Liner

> *"Distributed group expense and settlement engine on .NET 10, PostgreSQL, Kafka (KRaft), and Valkey. Implements debt graph minimisation with eventual consistency, channel-driven transactional outbox for atomic event publishing, idempotent command processing via custom mediator pipeline, payment-verified settlement via Razorpay webhooks with HMAC validation, real-time balance updates via SignalR with Valkey backplane, and horizontal scaling on AWS EKS with ALB, HPA, and read replicas."*

## A7 Interview Talking Points

1. Why PostgreSQL over MongoDB? Relational integrity, on-the-fly balance computation from normalised tables, JSONB for event payloads, EF Core migrations. Single database for both transactional data and event store.

2. Why custom mediator over MediatR? Same ISender/IRequestHandler/IPipelineBehavior abstractions in ~100 lines of reflection-based code. Zero NuGet dependency for a pattern that's straightforward to implement. Full control over the dispatch pipeline.

3. Why the outbox pattern? Eliminates the dual-write problem. The PostgreSQL write and the Kafka publish are atomic within a single EF Core transaction. No lost events even on pod crash. The OutboxPublisherService uses an in-memory Channel for near-instant relay, with a startup sweep for crash recovery.

4. How does idempotency work? Client generates a UUID per request in the X-Idempotency-Key header. The IdempotencyMiddleware checks Valkey — if the key exists, the cached response is returned. If not, the request proceeds and the response is cached with a 24-hour TTL. Only applies to POST requests.

5. Why Valkey locks for settlement? Razorpay retries webhooks up to 5 times. Without a distributed lock, two retries arriving within milliseconds can both see Pending status and both confirm the same settlement. The ValkeyDistributedLockService prevents concurrent webhook processing for the same settlement.

6. Are you SQL injection safe? Yes, structurally. EF Core generates parameterised queries. The value is passed as a separate parameter to PostgreSQL, never concatenated into SQL text.

7. How does rate limiting work? Two layers: AWS WAF for IP-level protection before requests hit your app; ASP.NET Core sliding window middleware for per-user limits using in-process state (Valkey-backed in production) so limits are shared across all pods.

8. How does this scale to millions of users? Each layer scales independently: Kubernetes HPA for API pods, PostgreSQL read replicas for query load, Valkey cluster sharding, Kafka partition parallelism, CloudFront CDN for frontend. PgBouncer for connection pooling.

9. What is the consistency model? Writes are strongly consistent (PostgreSQL transactions). Balance reads are strongly consistent (computed on-the-fly via BalanceCalculator). Debt graph updates are eventually consistent via Kafka consumers — typically \<500ms lag.

10. Why Valkey over Redis? Valkey is the Linux Foundation's open-source fork of Redis, wire-compatible, with no licensing concerns. AWS ElastiCache supports it natively. Same performance, same commands, open-source license.

# Section 7 — Complete Logic & User Flow Diagrams

All flows documented as Mermaid diagrams. Machine-readable by Claude Code, renderable by GitHub. Each flow covers the happy path, all decision branches, and a plain-text edge case list below it.

> *Claude Code instruction: read each Mermaid block to understand the exact sequence of operations. Edge case notes below each diagram cover every failure mode. Implement each flow exactly as diagrammed.*

## Flow 1 — User Onboarding (Registration & Login)

> ```mermaid
>
> flowchart TD
>
> A([User opens app]) --> B{Has account?}
>
> B -- No --> C[Sign Up screen]
>
> C --> D[Enter: name, email, password]
>
> D --> E{Client validation}
>
> E -- Invalid --> D
>
> E -- Valid --> F[POST /api/auth/register]
>
> F --> G{Email exists in DB?}
>
> G -- Yes --> H[HTTP 409 - email already registered]
>
> H --> C
>
> G -- No --> I[BCrypt hash pwd cost=12]
>
> I --> J[EF Core tx: INSERT user + INSERT outbox]
>
> J --> K[Issue JWT RS256 15min + refresh token 30d HttpOnly Secure SameSite=Strict cookie]
>
> K --> L([Dashboard])
>
> B -- Yes --> M[Sign In screen]
>
> M --> N[Enter email + password]
>
> N --> O{Rate limit: 5 req per 900s per IP}
>
> O -- Exceeded --> P[HTTP 429 with Retry-After header]
>
> O -- OK --> Q[POST /api/auth/login]
>
> Q --> QA{Account locked? locked_until in future}
>
> QA -- Locked --> QB[HTTP 401 - account locked]
>
> QA -- OK --> R{BCrypt.Verify constant-time}
>
> R -- Fail --> S[HTTP 401 generic message only]
>
> S --> T{MaxFailedAttempts reached?}
>
> T -- Yes --> U[Lock account for LockoutMinutes]
>
> T -- No --> N
>
> R -- Pass --> V[Reset failed_login_attempts Rotate refresh token + issue new JWT]
>
> V --> L
>
> L --> W{JWT expires mid-session?}
>
> W -- Yes --> X[Axios interceptor catches 401 POST /api/auth/refresh via HttpOnly cookie]
>
> X --> Y{Refresh token valid?}
>
> Y -- Already rotated --> Z[THEFT DETECTED via token_family: revoke ALL tokens in family Force re-login]
>
> Y -- Expired --> AA[Redirect to Sign In]
>
> Y -- Valid --> AB[New JWT + rotated refresh token old token marked replaced]
>
> AB --> AC[Retry original request transparently]
>
> ```

### Edge cases & notes:

- Never reveal in error messages whether an email exists. Always generic HTTP 401.

- BCrypt.Verify is constant-time — immune to timing attacks.

- Refresh token stored as SHA-256 hash in DB via TokenHasher — never plaintext.

- Reuse detection: already-rotated token presented again = all tokens in same token_family revoked immediately.

- Legacy PBKDF2 passwords: transparently rehashed to BCrypt on successful login.

- JWT uses RS256 asymmetric signing (RSA-2048 PEM key). Private key on server only.

- JWT payload: contains userId, email, name ONLY. No sensitive data in payload.

## Flow 2 — Group Creation & Joining

> ```mermaid
>
> flowchart TD
>
> A([Dashboard]) --> B{User action}
>
> B -- Create group --> C[Open Create Group sheet]
>
> C --> D[Enter: name, currency INR default, category]
>
> D --> E[POST /api/groups]
>
> E --> F[EF Core tx: INSERT group + INSERT member as admin + INSERT outbox GroupCreated]
>
> F --> G[Generate 8-char alphanumeric invite code Store in group row in PostgreSQL]
>
> G --> H([Group Detail screen])
>
> B -- Join group --> I[Tap invite link or enter code]
>
> I --> J[GET /api/groups/join/CODE No auth required - preview only]
>
> J --> K{Code matches a group?}
>
> K -- Invalid --> L[Show: invite link invalid]
>
> K -- Valid --> M[Show preview: name, member count, currency]
>
> M --> N{User confirms join?}
>
> N -- No --> A
>
> N -- Yes --> O[POST /api/groups/join/CODE Auth required]
>
> O --> P{Already a member?}
>
> P -- Yes --> Q[HTTP 200 current state - idempotent]
>
> P -- No --> R[EF Core tx: INSERT group_member + INSERT outbox MemberJoined]
>
> R --> S[OutboxPublisher relays MemberJoined to Kafka group-events]
>
> S --> T[SignalRDispatcherConsumer pushes to all existing members]
>
> S --> TA[EmailNotificationConsumer sends join notification email]
>
> T --> H
>
> Q --> H
>
> ```

### Edge cases & notes:

- Invite code stored in PostgreSQL group row. Admin can regenerate via POST /{id}/regenerate-invite — old code immediately replaced.

- Admin leaving: must promote another member first. Enforced in command handler.

- Group archival: is_archived = true, delete_after timestamp set. GroupArchiveCleanupJob (daily) hard-deletes past retention.

- Max group size: configurable via GroupOptions.MaxMembers in appsettings.json.

- Duplicate join: idempotent — returns HTTP 200 with current state, no duplicate event written.

## Flow 3 — Adding an Expense

> ```mermaid
>
> flowchart TD
>
> A([Group Detail - Expenses tab]) --> B[Tap + FAB button]
>
> B --> C[Open Add Expense sheet]
>
> C --> D[Enter: description, amount, paid-by, involved members, split type]
>
> D --> E{Split type}
>
> E -- Equal --> F[Auto-calculate: total_paise divided by N Remainder distributed penny-by-penny]
>
> E -- Exact --> G[User enters paise per person Live sum shown: X paise remaining]
>
> E -- Percentage --> H[User enters percent per person Must sum to exactly 100%]
>
> F --> I{Client validation passes?}
>
> G --> I
>
> H --> I
>
> I -- No --> D
>
> I -- Yes --> J[Generate UUID idempotency key client-side]
>
> J --> K[POST /api/groups/ID/expenses Header: X-Idempotency-Key: UUID]
>
> K --> L{Rate limit: 30 req per 60s per user write policy}
>
> L -- Exceeded --> M[HTTP 429]
>
> L -- OK --> N{IdempotencyMiddleware: key found in Valkey?}
>
> N -- Hit --> O[Return cached response Duplicate submission prevented]
>
> N -- Miss --> P[ValidationBehaviour: FluentValidation amount > 0, splits sum to total, all members belong to group]
>
> P -- Fail --> Q[HTTP 400 with per-property validation errors]
>
> P -- Pass --> PA[AuthorisationBehaviour: verify user is group member via IRequireGroupMembership]
>
> PA --> R[EF Core tx: INSERT expense + INSERT expense_splits N rows + INSERT stored_event ExpenseAdded + INSERT outbox_event + Cache idempotency response in Valkey 24h TTL]
>
> R --> RA[OutboxInterceptor fires OutboxChannel signal]
>
> RA --> S[OutboxPublisherService publishes ExpenseAdded to Kafka expense-events near-instantly]
>
> S --> T[DebtSimplifierConsumer: BalanceCalculator computes net balances then DebtSimplifier runs greedy reduction publishes DebtGraphUpdated]
>
> S --> U[SignalRDispatcherConsumer: push event to all connected group members]
>
> S --> UA[EmailNotificationConsumer: send expense notification email via SMTP]
>
> U --> W([All members see updated balances live])
>
> O --> W
>
> ```

### Edge cases & notes:

- ALL amounts stored as BIGINT paise. 100.50 INR = integer 10050. Zero decimals anywhere in codebase.

- Equal split remainder: distributed penny-by-penny. E.g. 100p / 3 = 33, 33, 34.

- Kafka partition key = groupId: sequential processing per group, parallel across groups.

- Outbox guarantees at-least-once delivery. Channel-driven for near-instant relay, startup sweep for crash recovery.

- Optimistic UI: expense appears in list immediately. Reverts with toast if server returns error.

- ExceptionHandlingMiddleware returns Problem+JSON (RFC 9110) for all error responses.

## Flow 4 — Editing and Deleting an Expense

> ```mermaid
>
> flowchart TD
>
> A([Expense detail]) --> B{Is user creator or group admin?}
>
> B -- No --> C[Edit and Delete buttons not shown]
>
> B -- Yes --> D{Action chosen}
>
> D -- Edit --> E[Open edit sheet pre-filled with current values]
>
> E --> I[PUT /api/groups/groupId/expenses/expenseId]
>
> I --> J[ValidationBehaviour + AuthorisationBehaviour]
>
> J -- Fail --> K[HTTP 400 or 401]
>
> J -- Pass --> L[EF Core tx: UPDATE expense + UPDATE splits + INSERT stored_event ExpenseEdited + INSERT outbox_event]
>
> L --> M[Kafka relays ExpenseEdited to expense-events]
>
> M --> N[DebtSimplifierConsumer recalculates debt graph]
>
> M --> O[SignalRDispatcherConsumer notifies all group members]
>
> M --> OA[EmailNotificationConsumer sends edit notification]
>
> D -- Delete --> T[DELETE /api/groups/groupId/expenses/expenseId]
>
> T --> U[EF Core tx: SET deleted_at soft-delete + INSERT stored_event ExpenseDeleted + INSERT outbox_event]
>
> U --> V[Kafka relays ExpenseDeleted Consumers recalculate and notify]
>
> ```

### Edge cases & notes:

- Soft delete only. deleted_at timestamp set. Data never hard-deleted from expenses table.

- BalanceCalculator excludes soft-deleted expenses (WHERE deleted_at IS NULL).

- If this is the last expense in the group after deletion: debt graph shows all settled up.

## Flow 5 — Debt Simplification Engine

> ```mermaid
>
> flowchart TD
>
> A([ExpenseAdded / ExpenseEdited / ExpenseDeleted event consumed from Kafka expense-events]) --> B[BalanceCalculator: query PostgreSQL for all non-deleted expenses, splits, and confirmed settlements for group]
>
> B --> C[Compute net balance per member: sum credits paid_by minus sum debits splits minus confirmed settlement payouts plus confirmed settlement receipts All BIGINT paise arithmetic only]
>
> C --> D{All balances equal zero?}
>
> D -- Yes --> E[Publish DebtGraphUpdated: empty plan - all settled up]
>
> D -- No --> F[DebtSimplifier: Split into CREDITORS positive balance and DEBTORS negative balance Sort both descending by absolute value]
>
> F --> G{Debtors list empty?}
>
> G -- No --> H[Take largest debtor D Take largest creditor C Transfer = min of abs-D and abs-C in paise]
>
> H --> I[Record: D pays C the Transfer amount]
>
> I --> J[D.balance += Transfer C.balance -= Transfer]
>
> J --> K{D.balance == 0?}
>
> K -- Yes --> L[Remove D from debtors list]
>
> K -- No --> M[D remains in list]
>
> L --> N{C.balance == 0?}
>
> M --> N
>
> N -- Yes --> O[Remove C from creditors list]
>
> N -- No --> P[C remains in list]
>
> O --> G
>
> P --> G
>
> G -- Yes --> Q[Publish DebtGraphUpdated with minimal transfer plan via outbox]
>
> Q --> R[SignalRDispatcherConsumer pushes updated debt graph to all members]
>
> ```

### Edge cases & notes:

- Circular debts: A owes B 500, B owes C 500, C owes A 500. Net balances all zero. Zero transfers needed.

- Floating point: IMPOSSIBLE. All arithmetic is BIGINT paise. Integer operations only.

- Kafka partition key = groupId: ordered processing per group. No concurrent runs for same group.

- Max result: N-1 transfers for N members (theoretical max). Usually far fewer.

- Algorithm complexity: O(N log N). Fast even for 50-member groups.

- Settlement plan query (GET /settlement-plan) also computes on-the-fly for strong consistency.

## Flow 6 — Settlement (Full Razorpay Payment Flow)

> ```mermaid
>
> flowchart TD
>
> A([User opens Settle Up tab]) --> B[GET /api/groups/ID/settlement-plan Computes on-the-fly via BalanceCalculator + DebtSimplifier]
>
> B --> C[Show simplified transfer cards: You pay Priya INR 1200 Rahul pays you INR 800]
>
> C --> D[User taps Pay Now on a transfer]
>
> D --> E[Confirmation sheet shown: who pays whom, amount, group name]
>
> E --> F{User confirms?}
>
> F -- No --> C
>
> F -- Yes --> G[POST /api/settlements/initiate]
>
> G --> GA[SettlementExpiryHelper: lazy-expire any stale Pending settlements]
>
> GA --> H[POST to Razorpay API: create order, amount_paise, currency INR]
>
> H --> I{Razorpay order created OK?}
>
> I -- Fail --> J[HTTP 502 - show retry button]
>
> I -- OK --> K[EF Core tx: INSERT settlement status=Pending razorpay_order_id stored expires_at set + INSERT outbox SettlementProposed]
>
> K --> L[Return razorpay_order_id to client]
>
> L --> M[Client loads Razorpay checkout SDK Opens payment sheet: UPI, Card, Net Banking]
>
> M --> N{User completes payment?}
>
> N -- Abandoned or closed --> O[Settlement stays Pending SettlementExpiryJob expires after configurable hours]
>
> N -- Payment submitted --> P[Razorpay processes payment]
>
> P --> Q{Payment result}
>
> Q -- Captured --> R[Razorpay fires webhook: POST /api/webhooks/razorpay payment.captured event]
>
> Q -- Failed --> S[Razorpay fires webhook: payment.failed event]
>
> R --> T{RazorpayWebhookVerifier: verify X-Razorpay-Signature HMAC-SHA256 CryptographicOperations.FixedTimeEquals}
>
> T -- Mismatch --> U[HTTP 400 Log security alert Process nothing]
>
> T -- Match --> V{Source IP in RazorpayOptions.AllowedWebhookIps? Empty list = allow all for dev}
>
> V -- Unknown IP --> W[HTTP 403 - log security alert]
>
> V -- Allowed --> X[ProcessWebhookCommandHandler: extract settlementId from payload]
>
> X --> Y[ValkeyDistributedLockService: acquire lock settlement:settlementId with configurable TTL]
>
> Y -- Cannot acquire --> Z[Skip - concurrent processing handled]
>
> Y -- Acquired --> AA{SELECT settlement WHERE id=settlementId AND status=Pending}
>
> AA -- Not Pending --> AB[Return HTTP 200 - idempotent already processed]
>
> AA -- Pending --> AC{Webhook amount == recorded settlement amount?}
>
> AC -- Mismatch --> AD[Set status=Review Flag for manual review]
>
> AC -- Match --> AE[EF Core tx: UPDATE settlement status=Confirmed razorpay_payment_id stored confirmed_at set + INSERT outbox SettlementConfirmed]
>
> AE --> AF[Release Valkey lock]
>
> AF --> AG[Kafka settlement-events topic]
>
> AG --> AH[SignalRDispatcherConsumer pushes SettlementConfirmed to all group members]
>
> AG --> AHA[EmailNotificationConsumer sends confirmation email via SMTP]
>
> AH --> AJ([Balances updated. Confetti animation.])
>
> S --> AK[Same HMAC + IP verification flow]
>
> AK --> AL[UPDATE settlement status=Failed + INSERT outbox]
>
> AL --> AM[SignalR notify: payment failed]
>
> AM --> C
>
> ```

### Edge cases & notes:

- HMAC MUST use CryptographicOperations.FixedTimeEquals NOT string ==. String == is a timing oracle.

- Razorpay retries webhooks up to 5x over 24h. Valkey lock + Pending check = idempotent processing.

- Second webhook delivery sees non-Pending status, returns HTTP 200 immediately, does nothing.

- User abandons checkout: Pending. SettlementExpiryJob (hourly) expires settlements past expires_at. SettlementExpiryHelper also lazy-expires on read paths.

- Amount mismatch: status=Review. Never auto-confirm wrong amount.

- Cancellation: POST /api/settlements/{id}/cancel — only payer, only while Pending.

- Local dev: ngrok for webhook testing. AllowedWebhookIps empty = allow all IPs.

## Flow 7 — Real-Time Updates (SignalR)

> ```mermaid
>
> flowchart TD
>
> A([User opens group screen]) --> B[Connect to SignalR hub /api/hubs/groups]
>
> B --> C[Hub validates JWT from query string]
>
> C --> D{JWT valid?}
>
> D -- No --> E[Reject connection HTTP 401]
>
> D -- Yes --> F[Client joins group:GROUP_ID channel]
>
> F --> H([Connected and listening for events])
>
> H --> I{SignalRDispatcherConsumer dispatches event from Kafka}
>
> I -- ExpenseAdded or Edited or Deleted --> J[Push to group:GROUP_ID All group members receive]
>
> I -- SettlementProposed --> K[Push to group:GROUP_ID]
>
> I -- SettlementConfirmed --> L[Push to group:GROUP_ID All members receive]
>
> I -- SettlementFailed --> M[Push to group:GROUP_ID]
>
> I -- MemberJoined or Left --> N[Push to group:GROUP_ID]
>
> I -- DebtGraphUpdated --> NA[Push to group:GROUP_ID]
>
> I -- BalanceUpdated --> NB[Push to group:GROUP_ID]
>
> J --> O[Client updates expense list and balance display]
>
> K --> P[Show notification banner]
>
> L --> Q[Trigger confetti animation Update balances live]
>
> M --> R[Show error toast with retry button]
>
> N --> S[Update member list and avatar stack]
>
> H --> T{Connection drops?}
>
> T --> U[Exponential backoff reconnect: 1s then 2s then 4s then 8s then max 30s]
>
> U --> V{Reconnected successfully?}
>
> V -- Yes --> W[Fetch REST delta since last-known state TanStack Query cache invalidation]
>
> W --> H
>
> V -- No after 5 attempts --> X[Show offline banner Keep retrying silently in background]
>
> ```

### Edge cases & notes:

- SignalR is ENHANCEMENT ONLY. REST API is always the source of truth.

- Multi-pod: Valkey backplane routes pushes from any pod to any connection.

- AWS ALB: cookie-based sticky sessions keep WebSocket connections on same pod.

- Multiple devices: all active connections for a user receive every push simultaneously.

- Offline user: misses the push entirely. Gets current state via REST on next app open.

- Event routing: all events flow through Kafka → SignalRDispatcherConsumer → GroupHubDispatcher → SignalR GroupHub.

## Flow 8 — Background Services

> ```mermaid
>
> flowchart TD
>
> A([Application starts]) --> B[IHostedService instances registered at startup]
>
> B --> C[OutboxPublisherService: startup sweep for unpublished events]
>
> B --> D[SettlementExpiryJob: Timer fires every 1 hour]
>
> B --> E[GroupArchiveCleanupJob: Timer fires every 24 hours]
>
> B --> BA[DebtSimplifierConsumer: subscribes to expense-events]
>
> B --> BB[SignalRDispatcherConsumer: subscribes to all topics]
>
> B --> BC[EmailNotificationConsumer: subscribes to relevant topics]
>
> C --> F[Query unpublished outbox_events WHERE published_at IS NULL]
>
> F --> G{Events found?}
>
> G -- No --> GA[Await OutboxChannel signal from OutboxInterceptor]
>
> GA --> F
>
> G -- Yes --> I[Publish each to Kafka via KafkaProducerService idempotence enabled]
>
> I --> J{Publish success?}
>
> J -- Fail --> K[Log error Event stays unpublished Retried on next signal or startup]
>
> J -- OK --> L[UPDATE outbox_events SET published_at = NOW WHERE id = eventId]
>
> L --> GA
>
> D --> M[SELECT settlements WHERE status=Pending AND expires_at < NOW]
>
> M --> N{Expired settlements found?}
>
> N -- No --> O[Done until next hour]
>
> N -- Yes --> P[UPDATE each status=Expired + INSERT outbox SettlementExpired]
>
> P --> Q[OutboxPublisher relays to Kafka SignalR notifies both parties Email notification sent]
>
> E --> R[SELECT groups WHERE is_archived=true AND delete_after < NOW]
>
> R --> S{Groups to hard-delete found?}
>
> S -- No --> T[Done until next day]
>
> S -- Yes --> U[Hard delete group and all related data]
>
> ```

### Edge cases & notes:

- OutboxPublisherService uses an in-memory Channel (bounded) for near-instant event relay. No polling interval.

- Startup sweep: on application start, queries for any unpublished events from prior crashes. Guarantees at-least-once delivery.

- OutboxInterceptor hooks into EF Core SaveChanges — fires Channel signal automatically when outbox events are written.

- SettlementExpiryHelper also provides lazy expiration on read paths (e.g., settlement initiation), so expiration doesn't solely depend on the background job.

- All Kafka consumers run as IHostedService with graceful shutdown via CancellationToken.

## Flow 9 — Complete End-to-End User Journey

> ```mermaid
>
> flowchart TD
>
> A([Rounak opens SettleUp]) --> B[Signs up with email and password]
>
> B --> C[Empty dashboard]
>
> C --> D[Creates group: Goa Trip, currency INR]
>
> D --> E[Shares invite link with Priya and Rahul]
>
> E --> F[Priya joins via link. Rahul joins via link.]
>
> F --> G[3 members in group. All balances zero.]
>
> G --> H[Rounak adds expense: Hotel 6000 INR Paid by Rounak Equal split 3 ways]
>
> H --> I[Outbox -> Kafka -> DebtSimplifierConsumer SignalRDispatcherConsumer pushes to Priya and Rahul instantly Each now owes Rounak 2000 INR]
>
> I --> J[Priya adds expense: Dinner 1500 INR Paid by Priya Equal split 3 ways]
>
> J --> K[Debt graph recalculated: Rounak net +1500 Priya net -500 Rahul net -2500 Wait... let me recalculate: Rounak paid 6000, owes 2000+500=2500, net +3500 Priya paid 1500, owes 2000+500=2500, net -1000 Rahul paid 0, owes 2000+500=2500, net -2500]
>
> K --> L[Settle Up tab shows simplified plan: Priya pays Rounak 1000 INR Rahul pays Rounak 2500 INR]
>
> L --> M[Priya taps Pay Now 1000 to Rounak]
>
> M --> N[Razorpay checkout opens Priya pays via UPI]
>
> N --> O[Razorpay fires webhook to SettleUp]
>
> O --> P[HMAC verified Valkey lock acquired Status Pending -> Confirmed]
>
> P --> Q[Priya balance updates to zero live via SignalR Confetti on both screens]
>
> Q --> R[Rahul taps Pay Now 2500 to Rounak]
>
> R --> S[Same Razorpay flow runs]
>
> S --> T([All balances zero. Group shows: All settled up!])
>
> ```

## Flow 10 — Error States & Recovery

> ```mermaid
>
> flowchart TD
>
> A([Error occurs]) --> B{Error type}
>
> B -- Network offline --> C[Show offline banner Queue writes locally Retry on reconnect]
>
> B -- JWT expired mid-session --> D[Axios interceptor catches 401 Refresh token silently via HttpOnly cookie Retry original request User sees nothing]
>
> B -- Refresh token expired or revoked --> E[Redirect to Sign In Clear all local state Show: session expired message]
>
> B -- Rate limited HTTP 429 --> F[Show Retry-After countdown timer Disable submit button until timer expires]
>
> B -- Razorpay checkout fails --> G[Settlement stays Pending Show Retry and Cancel buttons]
>
> G -- Retry --> H[Reopen Razorpay checkout Same settlement ID and order]
>
> G -- Cancel --> I[POST /api/settlements/ID/cancel Status set to Cancelled]
>
> B -- Duplicate submit --> J[IdempotencyMiddleware returns cached response No duplicate created]
>
> B -- Pod crashes mid-transaction --> K[Outbox event survives in PostgreSQL OutboxPublisherService startup sweep picks up Redis lock TTL prevents double-process User sees delayed confirmation not duplicate]
>
> B -- PostgreSQL primary failover --> L[RDS Multi-AZ auto-failover 60-90 seconds Retry with exponential backoff If all fail: HTTP 503 to client]
>
> B -- Kafka consumer lag --> M[Balances always consistent via on-the-fly BalanceCalculator Debt graph slightly stale in SignalR Typically resolves in under 500ms]
>
> B -- Validation error --> N[ExceptionHandlingMiddleware returns Problem+JSON HTTP 400 with per-property error details]
>
> ```

### Data Flow Summary

The complete lifecycle from user tap to database write to real-time update:

> User action (Next.js 15 App Router)
>
> |
>
> | HTTPS via AWS ALB
>
> | - SSL termination
>
> | - Layer 7 path routing (/api/\* -> .NET pods, /\* -> Next.js pods)
>
> | - Health checks, sticky sessions for SignalR WebSockets
>
> | [AWS WAF sits in front: DDoS, OWASP rules, IP rate limiting]
>
> |
>
> v
>
> ASP.NET Core Web API (.NET 10)
>
> |
>
> +-- Rate Limit Middleware (sliding window, per-user per-endpoint limits)
>
> +-- JWT Auth Middleware (RS256 RSA-2048 validation, every endpoint)
>
> +-- IdempotencyMiddleware (Valkey key check, POST requests with X-Idempotency-Key)
>
> +-- ExceptionHandlingMiddleware (Problem+JSON responses)
>
> |
>
> v
>
> Custom Mediator Pipeline
>
> |
>
> +-- ValidationBehaviour (FluentValidation on every command)
>
> +-- AuthorisationBehaviour (IRequireGroupMembership check)
>
> |
>
> v
>
> Command or Query Handler
>
> |
>
> +-- WRITE PATH:
>
> | EF Core Transaction (PostgreSQL primary, parameterised queries)
>
> | |- INSERT business data (expenses, settlements, etc.)
>
> | |- INSERT stored_event (event log)
>
> | \`- INSERT outbox_event (same transaction = atomic)
>
> | OutboxInterceptor fires OutboxChannel signal
>
> | Return HTTP response to client
>
> |
>
> | OutboxPublisherService (channel-driven, near-instant)
>
> | -> Publish to Kafka (partition key = groupId)
>
> | |- DebtSimplifierConsumer -> BalanceCalculator + DebtSimplifier -> DebtGraphUpdated
>
> | |- SignalRDispatcherConsumer -> GroupHubDispatcher -> SignalR push to clients
>
> | \`- EmailNotificationConsumer -> SmtpEmailService -> HTML email delivery
>
> |
>
> \`-- READ PATH:
>
> Query Handler
>
> BalanceCalculator computes on-the-fly from PostgreSQL tables
>
> Strongly consistent — always reflects latest committed state
>
> Razorpay Webhook (server-to-server, external):
>
> POST /api/webhooks/razorpay
>
> -> RazorpayWebhookVerifier: HMAC-SHA256 (CryptographicOperations.FixedTimeEquals)
>
> -> RazorpayWebhookVerifier: Source IP check (if AllowedWebhookIps configured)
>
> -> ValkeyDistributedLockService: acquire lock settlement:{settlementId}
>
> -> SELECT settlement WHERE status=Pending
>
> -> EF Core tx: UPDATE settlement + INSERT outbox
>
> -> Kafka -> SignalRDispatcherConsumer -> SignalR push to all group members
>
> -> Kafka -> EmailNotificationConsumer -> SMTP email notification

### Security Checklist for Claude Code

> *Claude Code: enforce ALL items below in every file generated. These are non-negotiable constraints.*

- Monetary values: BIGINT paise ONLY. Never float, double, or decimal for any money amount.

- Database: EF Core LINQ or ExecuteSqlInterpolated ONLY. Never ExecuteSqlRaw with string concatenation.

- Webhook HMAC: CryptographicOperations.FixedTimeEquals ONLY. Never string == or .Equals() for signatures.

- Rate limiting: applied to all /api/auth/\* endpoints and all write endpoints (POST/PUT/DELETE).

- Idempotency: X-Idempotency-Key header on POST commands. Checked in IdempotencyMiddleware (Valkey-backed).

- JWT auth: every endpoint except /api/auth/\*, /api/groups/join/{code} preview, and /api/webhooks/razorpay requires valid JWT.

- Group membership: AuthorisationBehaviour validates requesting user is a member via IRequireGroupMembership marker interface.

- Settlement authorisation: only the payer (debtor side) can initiate their own settlement.

- Secrets: use strongly-typed IOptions\<T\> configuration. In production, inject from AWS Secrets Manager.

- Outbox: domain events ONLY via outbox table in EF Core transaction. Never call KafkaProducerService directly in command handlers.

- Input validation: FluentValidation on every command via ValidationBehaviour pipeline. Reject at the boundary, not deep in domain logic.

- CORS: explicit allowed origins only. No wildcard \* in production ever.

- HTTPS: HSTS header enforced. ALB redirects all HTTP to HTTPS.

- Password hashing: BCrypt with configurable cost factor. Support legacy PBKDF2 migration.

- Token storage: SHA-256 hash only. Never store plaintext refresh tokens.

*End of document — SettleUp v2.0 (Sections 1–7)*
