# ARCHITECTURE.md — System Architecture Standard & Decision Framework

> **Status:** Mandatory architecture standard for all engineers and AI agents in this repo.
> **Goal:** Systems that are correct, scalable *to their actual needs*, fast, observable, and
> cheap to change — derived deliberately, not cargo-culted.
> **Priority:** Correctness > Simplicity > Observability > Performance > Scalability > Novelty.
>
> **READ THIS FIRST — the most important sentence in the file:**
> There is no universal "best" architecture. The best architecture is the **simplest one that
> meets this project's actual requirements** (load, latency, *criticality*, consistency, team
> size, budget, compliance). "Google-scale" patterns applied to a non-Google-scale problem are
> not advanced — they are a **defect** (over-engineering). Equally, under-building a high-stakes
> system because its traffic is low is the **same defect inverted**. This doc helps you derive
> the right architecture, tells you when "a boring monolith on one server" is the 10/10 answer
> (it often is), and — via §18 — makes the result *verifiable* rather than merely claimed.

---

## 0. How to Use This File

- **New project:** run §2 (Requirements) → §3 (Decision Framework). Stop at the simplest tier
  that meets requirements — unless §3's criticality/compliance override applies. Record decisions
  as ADRs (§14).
- **Existing project:** run §15 (Audit & Refactor playbook) to assess and incrementally fix —
  **never rewrite from scratch by default** (§15 explains why rewrites usually fail).
- **Verification:** §18 turns architectural *claims* into machine-checked *facts*. A property you
  can't verify, you haven't achieved.
- **AI agents:** §16 is binding. It forbids over-engineering as strongly as under-engineering,
  and forbids claiming unverified properties.
- **Humans:** §1 (principles) + §3 (framework) are the core. The rest is reference.

---

## 1. First Principles (the reasoning the FAANG companies actually use)

These outlast any specific technology.

1. **Simplicity is the prerequisite for reliability.** (Hoare/Dijkstra.) You cannot operate
   what you cannot understand. Complexity is a cost paid forever, not once.
2. **Build for the load you have + one foreseeable order of magnitude — not for imagined scale.**
   Premature scaling is premature optimization wearing a bigger suit.
3. **Boring technology wins — but don't reflex-reject a warranted specialized tool.** Prefer
   proven, well-understood tools (Postgres, a single well-run service) over novel/fashionable
   ones, and spend your limited "innovation tokens" deliberately (Dan McKinley). *However:*
   reflexively rejecting a genuinely-needed specialized store (real time-series, graph,
   full-text, geo, vector workloads) is the same error as reflexively adopting an unwarranted
   one. The burden is "does the access pattern truly need it?" — not "is it boring?"
4. **Design for change, because requirements will.** Loose coupling + high cohesion + clear
   boundaries. Optimize for *changeability*, which dominates lifetime cost.
5. **Make it observable or it doesn't exist.** If you can't measure latency, errors, and
   saturation, you're guessing. Observability is not optional.
6. **Everything fails; design for failure.** Networks partition, disks die, dependencies time
   out. Decide *how* it fails (graceful degradation, retries, timeouts, idempotency).
7. **Data is the hardest thing to change.** Code is cheap to refactor; schemas, storage
   choices, and data models are expensive and risky. Get the data model right; everything
   else is reversible.
8. **Decisions are reversible (Type 2) or not (Type 1).** Move fast on reversible decisions;
   deliberate carefully on irreversible ones (data model, core API contracts, auth model,
   primary datastore). (Bezos "one-way/two-way doors.")
9. **Conway's Law is real.** Your architecture will mirror your team/communication structure.
   Design boundaries that match how humans will actually own them.
10. **Measure, don't guess.** Profile before optimizing. The bottleneck is rarely where you think.

---

## 2. Requirements Capture (do this BEFORE choosing anything)

You cannot design without these. If unknown, **estimate and state the estimate** — do not
silently assume. Architecture is a function of these numbers *and* of criticality.

```
CRITICALITY (this axis can OVERRIDE the load-based tier model — see §3)
- Blast radius of a single wrong result: cosmetic | costly | irreversible | life-safety
- Is correctness-dominated? (money/ledgers, medical, legal, safety, anything irreversible
  or regulated) — if yes, rigor is dictated by stakes, NOT by traffic.

SCALE
- Users: current ___ / realistic 12-month ___
- Requests/sec: average ___ / peak ___
- Data volume: now ___ / growth/month ___
- Read:write ratio: ___ (most systems are read-heavy — this drives caching/replicas)

PERFORMANCE TARGETS (set explicit SLOs)
- Latency: p50 ___ / p95 ___ / p99 ___   (p99 is what users feel; design for the tail)
- Availability target: ___ (99.9% = 43m/mo down; 99.99% = 4m/mo — each 9 multiplies cost)
- Throughput required: ___

CONSISTENCY & CORRECTNESS
- Strong consistency required? (money, inventory, bookings = yes)
- Or is eventual consistency acceptable? (feeds, analytics, counts = usually yes)

CONTEXT
- Team size & experience: ___ (a 2-person team CANNOT operate 30 microservices)
- Budget / cost ceiling: ___
- Compliance/data residency: (PII, PCI, HIPAA, GDPR, region constraints — these can force
  topology, e.g. multi-region, regardless of user count)
- Integration surface: (number/criticality of third-party contracts — sometimes the hard part)
- Deadline / stage: (prototype | MVP | growth | scale)
```

> **The numbers decide the architecture — but criticality and compliance can override them.**
> 100 req/s and 100,000 req/s are different universes. So are "a cosmetic bug is annoying" and
> "a wrong result is irreversible." Match *both* axes.

---

## 3. The Architecture Decision Framework (choose the SIMPLEST tier that fits)

Walk the tiers in order. **Stop at the first tier that meets your §2 requirements.** Climbing
higher than needed is over-engineering; it's a defect, not sophistication.

### Tier 0 — Prototype / MVP (almost every new project starts here)
- **Modular monolith**, single deployable, one relational database (Postgres).
- Server-rendered or SPA + one API. Background work via a simple queue/cron if needed.
- One region, vertical scaling, managed hosting (Render/Fly/Railway/Vercel/a single VM).
- **This is the correct 10/10 answer for most projects for a long time.** Ship, learn, measure.

### Tier 1 — Growth (real users, measured bottlenecks)
- Still a **modular monolith** (or 2–3 services at most, split only along proven seams).
- Add: read replicas, a cache (Redis), a proper queue (SQS/RabbitMQ) for async work, a CDN.
- Horizontal scaling of stateless app servers behind a load balancer. Connection pooling.
- Introduce these **only when a metric demands it**, not preemptively.

### Tier 2 — Scale (sustained high load, large team, proven need to split)
- Decompose along **bounded contexts** (DDD) into services — **only** where team ownership or
  independent scaling genuinely requires it. Each service owns its data (no shared DB).
- Async/event-driven between contexts (Kafka/Kinesis/SNS+SQS); idempotent consumers.
- Sharding/partitioning where a single DB is the proven bottleneck. CQRS where read/write
  profiles truly diverge.
- Multi-AZ; consider multi-region only for real latency/availability/compliance needs.

### Tier 3 — Hyperscale (you are operating at the size where you'd have your own SREs)
- Cell-based architecture, global distribution, data sharding strategy, dedicated platform
  teams. **If you're reading this doc to learn, you are not here yet.** Hire/consult specialists.

> ### ⚠️ CRITICALITY & COMPLIANCE OVERRIDE THE TIERS
> The tiers above are indexed on **LOAD**. Load is not the only axis of hardness. A system can
> require **Tier-2/3 RIGOR at Tier-0 LOAD** when:
> - **Correctness is irreversible / high-stakes** (money, medical, legal, safety, regulated). A
>   wrong result at 5 req/s is still catastrophic. Demand, regardless of traffic: strong
>   consistency, explicit invariants/constraints enforced at the data layer, full audit trails,
>   exhaustive and adversarial testing, redundancy and DR beyond what the load implies, and
>   careful Type-1 deliberation on every data and contract decision.
> - **Compliance / data-residency forces topology** (e.g., multi-region or data-isolation at 10
>   users for GDPR / data-sovereignty / sector regulation).
> - **Integration complexity dominates** (the hard part is 14 third-party contracts and their
>   failure modes, not throughput) — design for those boundaries, not for req/s.
>
> **Under-engineering a high-stakes, low-traffic system is the SAME defect as over-engineering a
> low-stakes, high-traffic one.** Right-sizing means matching BOTH axes — load *and* criticality —
> not just the load numbers. When in doubt on a high-stakes domain, escalate rigor and ASK.

> **The Monolith-First Rule (Fowler, Newman, and hard-won industry consensus):** Start with a
> well-structured **modular monolith**. Microservices are an *answer to organizational scaling
> problems you don't have yet*, and they trade simplicity for a distributed-systems tax
> (network failures, eventual consistency, distributed tracing, deployment complexity, data
> consistency across services). **Do not start with microservices.** Extract services later,
> from a clean monolith, when a real seam and a real reason appear. (Note: a "modular monolith"
> is only real if §4's boundaries are *enforced* by §18 tooling — otherwise it's a ball of mud
> with a nicer name.)

---

## 4. Layering & Boundaries (applies at every tier)

A clean internal structure makes a monolith great *and* makes future extraction cheap.

- **Separate by responsibility, depend in one direction** (Clean/Hexagonal/Onion architecture —
  same core idea):
  ```
  Inbound (HTTP/gRPC/CLI/jobs)  →  Application/Use-cases  →  Domain (business rules)
                                          ↓
                                Ports/Interfaces  →  Adapters (DB, cache, queue, 3rd-party)
  ```
- **Domain layer has zero framework/IO dependencies.** Business rules don't import the ORM,
  HTTP, or vendor SDKs. This is what keeps logic testable and portable.
- **Dependency rule:** dependencies point *inward*. Outer layers know inner; inner never knows outer.
- **High cohesion, low coupling.** Group by *feature/bounded-context*, not by technical type
  (prefer `features/billing/*` over a giant `controllers/`, `services/`, `models/` split once
  the app grows).
- **Stable contracts at boundaries:** explicit interfaces (ports) for every external dependency
  so you can swap Postgres↔X or Stripe↔Y without touching the domain.
- **No circular dependencies.** Enforce with tooling (dependency-cruiser, import-linter, ArchUnit) — see §18.

---

## 5. Data Architecture (the highest-stakes, least-reversible decisions — §1.7)

- **Default to a relational database (Postgres).** It scales far further than people think,
  gives you ACID, joins, constraints, and JSON when you need flexibility. Reach for NoSQL or a
  specialized store only for a *specific proven access pattern* (massive scale-out, document/
  graph/time-series/geo/vector/full-text shape) — not by default, but also not reflexively
  refused when the pattern truly calls for it (§1.3). Polyglot persistence is a Tier-2 concern,
  not a starting point.
- **Model the data deliberately:** normalize for write integrity; denormalize *selectively* and
  *measurably* for read performance — never preemptively.
- **Indexing:** index columns used in WHERE/JOIN/ORDER BY; watch write-cost of over-indexing;
  use covering/composite indexes for hot queries; verify with `EXPLAIN ANALYZE`.
- **Avoid the classic killers:** N+1 queries (batch/join), unbounded `SELECT *`, missing
  pagination, full-table scans on big tables, chatty per-row calls.
- **Transactions** for multi-step invariant-preserving writes. Choose the right isolation level
  consciously. Keep transactions short. (For high-criticality domains, enforce invariants as DB
  constraints, not just app code — §3 override.)
- **Consistency model is a design decision, not a default:** strong where correctness demands
  (money/inventory/auth); eventual where it's fine (feeds/counts/analytics). Understand CAP/PACELC —
  under partition you trade consistency vs. availability; even without partitions you trade
  latency vs. consistency.
- **Migrations:** versioned, forward-only, backward-compatible (expand→migrate→contract). Never
  a destructive migration without a tested rollback path. Migrations are SAFE-MODE work.
- **Caching is a correctness problem, not just a speed trick:** cache only with an explicit TTL
  and/or invalidation strategy. Know the staleness you're accepting. Cache the expensive and the
  hot, not everything. (Cache-aside is the safe default.)
- **Scaling order (do in this sequence, each only when measured):** optimize queries/indexes →
  connection pooling → vertical scale → read replicas → caching → partition/shard (last, it's
  the most painful and least reversible).
- **Backups & recovery:** automated, encrypted, access-controlled, and **restore-tested**
  (an untested backup is not a backup). Define RPO/RTO explicitly.

---

## 6. API & Contract Design

- **Pick the right protocol for the consumer:** REST (broad, cacheable, simple) is the default;
  gRPC for internal high-throughput/low-latency service-to-service; GraphQL when clients need
  flexible aggregation (and you accept its complexity/abuse-surface).
- **Contracts are near-irreversible (§1.8). Version them.** Never make breaking changes to a
  published contract without a version + deprecation path.
- **Design rules:** consistent resource naming; correct status codes; explicit error schema
  (machine-readable code + human message + correlation id); pagination on every list (default +
  hard max); strict request schemas (reject unknown fields); idempotency keys for unsafe
  retryable operations.
- **Backward compatibility:** additive changes are safe; removals/renames/type-changes are
  breaking. Tolerant reader pattern on the consumer side.
- **Document the contract** (OpenAPI/protobuf) as the source of truth; generate types from it.

---

## 7. Communication & Coupling Between Components

- **Synchronous (request/response)** when the caller needs an immediate answer and the callee is
  fast and reliable. Always with **timeouts, retries (with jitter+backoff), and circuit breakers** —
  never an unbounded synchronous call to a dependency.
- **Asynchronous (events/queues)** to decouple, absorb spikes, and isolate failure. Prefer async
  for anything that can be eventually consistent (emails, notifications, downstream updates).
- **Events:** consumers must be **idempotent** (messages get redelivered). Define delivery
  semantics (at-least-once is the realistic default). Use a dead-letter queue for poison messages.
- **Avoid distributed transactions.** Use the **Saga pattern** (orchestrated or choreographed)
  with compensating actions for multi-service workflows; or the **Outbox pattern** to publish
  events atomically with a DB write. Two-phase commit is a last resort.
- **Minimize chattiness** across network boundaries; a network call is ~6 orders of magnitude
  slower than an in-process call. This is *the* hidden cost of premature microservices.

---

## 8. Performance & Scalability (measure, then act)

- **Stateless app servers** → horizontal scale trivially behind a load balancer. Push state to
  the datastore/cache/session store. Statelessness is the single highest-leverage scalability property.
- **Latency budget:** allocate a p99 budget across the request path (e.g., LB + app + DB +
  external). Optimize the actual tail, found via profiling — not your guess.
- **Scaling levers in order of preference:** (1) do less work (caching, better queries,
  algorithmic fixes) → (2) vertical scale (simplest) → (3) horizontal scale stateless tier →
  (4) async/offload to queues → (5) read replicas → (6) shard/partition data (last resort).
- **Concurrency & I/O:** prefer async/non-blocking I/O for I/O-bound work; bound all pools
  (threads, DB connections, HTTP clients); apply backpressure (don't accept work you can't handle).
- **Caching layers** (closest-first): client/HTTP cache → CDN (static + cacheable responses) →
  application/in-memory → distributed cache (Redis) → DB query cache. Each needs an
  invalidation story.
- **Load management:** rate limiting, load shedding (fail fast under overload rather than
  collapse), graceful degradation (serve a reduced experience over an error), autoscaling on the
  *right* signal (queue depth/latency, not just CPU).
- **Hot-path discipline:** identify the few critical paths; optimize those relentlessly; leave
  the rest simple. Don't optimize cold paths.
- **Don't optimize prematurely (Knuth):** correctness and clarity first; measured optimization second.

---

## 9. Reliability & Resilience (everything fails — §1.6)

- **Eliminate single points of failure** at the tier your SLO requires (redundancy, multi-AZ).
  Don't over-provision beyond your availability target — each extra "9" costs exponentially.
  (Exception: high-criticality systems per §3 may justify redundancy beyond their load profile.)
- **Timeouts on every external call.** No unbounded waits, ever.
- **Retries** with exponential backoff + jitter, **and** idempotency, **and** a cap. Retrying a
  non-idempotent op or retrying without backoff causes retry storms / cascading failure.
- **Circuit breakers + bulkheads** to isolate a failing dependency so it can't sink the whole system.
- **Graceful degradation & fallbacks:** a partial/cached/read-only experience beats a hard error.
- **Health checks** (liveness vs. readiness — they're different) for orchestrators/LBs.
- **Graceful shutdown:** drain in-flight requests, finish/requeue jobs, close pools cleanly.
- **Disaster recovery:** defined RPO/RTO, tested restores, runbooks. Practice failure
  (game days / chaos testing) at higher tiers.

---

## 10. Security Architecture (cross-references SECURITY.md)

- **Zero-trust posture (NIST 800-207):** authenticate & authorize every request; never trust the
  network perimeter alone. Service-to-service calls are authenticated too.
- **Defense in depth + least privilege** at every layer (network, service, data). Each component
  gets the minimum access it needs.
- **AuthZ at the data layer**, not just the route (ownership/tenant checks). Multi-tenant systems
  enforce tenant isolation at the query/row level (RLS) — a top blast-radius concern.
- **Secrets in a manager**, never in code/config/logs (see SECURITY.md). Encrypt in transit
  (TLS 1.2+/1.3) and at rest.
- **Validate at trust boundaries; parameterized queries only; fail closed.** (Full detail:
  SECURITY.md.)
- **Audit logging** of security-relevant events to an append-only store.
- Architecture and security are not separable — every boundary in §4 is also a trust boundary.

---

## 11. Observability (you can't operate what you can't see — §1.5)

The three pillars + the practice:
- **Logs:** structured (JSON), leveled, with a **correlation/trace id** threaded through every
  request and across services. No secrets/PII (SECURITY.md). Centralized, queryable.
- **Metrics:** the **Four Golden Signals** (Google SRE) — **Latency, Traffic, Errors,
  Saturation** — per service/endpoint. Plus business metrics. Dashboards + alerting on SLOs.
- **Traces:** distributed tracing (OpenTelemetry) across boundaries so you can see *where* the
  p99 latency actually goes. Essential the moment you have >1 service.
- **SLOs & error budgets:** define them (§2), measure against them, alert on **symptoms users
  feel** (SLO burn, error rate, latency), not just causes (CPU). Avoid alert fatigue.
- **OpenTelemetry as the vendor-neutral default** so you're not locked to one APM.
- **Observability is the prerequisite for any "continuous optimization"** — without standing
  telemetry, optimization is guessing (see §16.12).

---

## 12. Deployment, Environments & Operations

- **Infrastructure as Code** (Terraform/Pulumi/CDK) — environments are reproducible, reviewed,
  versioned. No click-ops in production.
- **CI/CD:** automated build → test → security scan → deploy. Fast, automated, reversible.
- **Safe deploys:** blue-green or canary or rolling with health-gated rollout and **automatic
  rollback** on SLO regression. Deploys are routine and reversible, not events.
- **Decouple deploy from release:** feature flags let you ship code dark and release gradually,
  and kill a bad feature without a redeploy.
- **Immutable, reproducible artifacts** (containers/images); pin versions; one artifact promoted
  across environments (don't rebuild per env).
- **Environment parity:** dev/staging/prod as similar as practical to kill "works on my machine."
- **Right-size the platform:** a managed PaaS or a single orchestrated host is correct at Tier
  0–1. **Don't adopt Kubernetes to run three containers** — it's an operational tax you pay
  forever. Adopt it when scale/team genuinely justifies it.
- **Cost is an architectural constraint:** track it; the cheapest architecture that meets SLOs
  wins. Idle complexity is wasted money.

---

## 13. Anti-Patterns Kill List (architecture's version of "looks AI-generated")

Avoid all unless there's a deliberate, stated, requirements-backed reason:

**Over-engineering (the dominant failure mode for ambitious projects):**
- ❌ Microservices for a small team / small load / day one.
- ❌ Kubernetes / service mesh / Kafka to solve problems you don't have yet.
- ❌ Multi-region / sharding / CQRS / event-sourcing without a measured need.
- ❌ Abstractions for imagined future requirements ("we might need to swap DBs"). YAGNI.
- ❌ Adding a new datastore/queue/tool per feature (operational sprawl).
- ❌ Chasing novel/trendy tech over boring proven tech (spending innovation tokens carelessly).

**Under-engineering / sloppiness (equally a defect):**
- ❌ Big ball of mud — no boundaries, everything imports everything (a "modular monolith" with
  no enforced boundaries IS this — see §18).
- ❌ Shared mutable database between would-be-separate services (distributed monolith — worst of both).
- ❌ Business logic in controllers/UI; framework code leaking into the domain.
- ❌ No timeouts, no retries-with-backoff, no idempotency, synchronous chains with no breakers.
- ❌ N+1 queries, unbounded queries, no pagination, no indexes.
- ❌ No observability; debugging by `print` in production.
- ❌ Destructive migrations with no rollback; untested backups.
- ❌ Secrets in code; auth checked only at the route.
- ❌ **Under-building a high-stakes domain because traffic is low** (the §3 override failure):
  no audit trail / weak consistency / thin testing on money/medical/legal/safety logic.

**Distributed-systems traps (when you do go multi-service):**
- ❌ Distributed monolith (services that must deploy together / share a DB).
- ❌ Synchronous service call chains (latency multiplies; one slow dep stalls all).
- ❌ Non-idempotent event consumers; no dead-letter handling.
- ❌ Distributed transactions / 2PC instead of sagas/outbox.

---

## 14. Architecture Decision Records (ADRs) — the source of truth for "why"

Every significant/irreversible (Type-1) decision gets a short ADR in `/docs/decisions/`.
This is how Google/Amazon/Microsoft retain reasoning and onboard people. Template:

```
# ADR-000X: <decision title>
Status: Proposed | Accepted | Superseded by ADR-00Y
Date: YYYY-MM-DD
Context: <problem, requirements from §2 (incl. criticality), constraints, forces>
Decision: <what we chose>
Consequences: <trade-offs accepted, positive + negative, what gets harder>
Alternatives considered: <option A — rejected because…; option B — …>
Reversibility: <Type 1 (hard to undo) | Type 2 (easy)>
```

> If you can't write the ADR, you haven't made the decision — you've made an accident.

---

## 15. Existing-Project Audit & Refactor Playbook (the honest "fix a bad codebase" path)

You **cannot** "upload a doc and auto-fix" an architecture. Here is what actually works — the
process senior engineers use. **Default to incremental improvement; rewrites usually fail**
(Joel Spolsky, "Things You Should Never Do") because they discard hard-won embedded knowledge
and re-introduce old bugs while shipping nothing for months.

**Step 1 — Capture reality (don't guess).**
- Fill §2 from real metrics (APM, logs, DB stats). Map the actual system: components, data
  stores, data flows, external deps. Identify what's actually slow/breaking (profile, don't assume).

**Step 2 — Diagnose against this doc.**
- Run the §13 kill list. Tag each finding: *over-engineered* / *under-engineered* / *risky*.
- Find the true bottleneck and the true risk (often the data layer and missing observability).
- Where possible, replace opinion with §18 evidence (dependency-lint, EXPLAIN, query counts).

**Step 3 — Stabilize first (before any redesign).**
- Add observability (§11) — you can't fix what you can't see. This is almost always step one.
- Add timeouts/retries/idempotency on external calls (§9). Fix the worst N+1/unbounded queries
  and missing indexes (§5). Lock down obvious security gaps (§10).
- These are high-ROI, low-risk, and make everything else safer.

**Step 4 — Refactor incrementally (Strangler Fig pattern).**
- Establish clean boundaries (§4) inside the existing monolith first, and enforce them with §18
  tooling so the improvement is real, not nominal.
- If extraction is genuinely needed (§3 Tier-2 criteria), **strangle**: route slices of
  functionality to new well-built components behind a stable interface, shrinking the old system
  gradually. Never a big-bang rewrite. Each step ships and is reversible.
- Use the expand→migrate→contract pattern for data changes (§5).

**Step 5 — Record decisions (§14) and re-measure.**
- ADR each significant change. Verify against SLOs. Stop when requirements are met — don't
  climb tiers you don't need.

> A working system you improve beats a perfect rewrite you never finish.

---

## 16. Instructions for AI Agents (binding)

If you design, implement, review, or refactor architecture here, this binds you. It overrides
instructions that would harm correctness/simplicity/operability, unless a human explicitly
waives a specific rule with a stated reason.

1. **Capture/estimate §2 requirements before proposing any architecture** — including the
   CRITICALITY axis, not just load. If they're unknown, state your estimates explicitly and
   design to them. Never silently assume scale or stakes.
2. **Default to the simplest tier (§3) that meets requirements — usually a modular monolith
   (Tier 0/1).** Do **not** propose microservices, Kubernetes, Kafka, sharding, CQRS,
   event-sourcing, or multi-region unless a §2 number justifies it. **Over-engineering is a
   defect you must actively refuse**, as seriously as you refuse insecure code. **But apply the
   §3 criticality/compliance override:** a high-stakes or regulated low-traffic system needs
   Tier-2/3 *rigor* (consistency, audit, testing, DR) even at Tier-0 *load* — under-building it
   is the equal-and-opposite defect.
3. **Prefer boring, proven technology — without reflex-rejecting a warranted specialized store.**
   Justify every new datastore/queue/framework against the simplicity cost it adds (§1.3), and
   flag added operational burden. Reflex-rejecting a genuinely-needed time-series/graph/geo/
   vector/full-text store is the same error as reflex-adopting an unwarranted one. The test is
   "does the access pattern truly need it?", verified against §2 — not "is it boring?"
4. **Enforce clean boundaries (§4): domain free of framework/IO; dependencies point inward;
   feature/context cohesion; no cycles.** A "modular monolith" is only real if §18 tooling
   enforces this; otherwise do not call it one.
5. **Treat data decisions as near-irreversible (§5):** relational by default; deliberate schema;
   indexed/paginated/bounded queries; transactions (and DB-level constraints for high-criticality
   invariants); backward-compatible, reversible migrations (SAFE MODE).
6. **Build in resilience and observability by default (§9, §11):** timeouts + backoff-retries +
   idempotency on external calls; structured logs with correlation ids; the four golden signals.
   A design without observability is incomplete.
7. **For existing projects, follow §15:** capture reality → diagnose → stabilize → strangle
   incrementally. **Never propose a from-scratch rewrite as the default** — recommend incremental
   refactor and explain why, unless the human explicitly accepts rewrite risk.
8. **Write an ADR (§14) for every significant/irreversible decision.** No silent architectural choices.
9. **Honesty requirement:** do not claim a design is "Google/Amazon-scale," "infinitely
   scalable," or "production-ready" beyond what the requirements need and what you actually
   built/verified. State assumptions, the tier you targeted and why, trade-offs accepted, and
   what still needs human review or load-testing. **Right-sized beats impressive.**
10. **When asked to over-build** ("make it microservices / web-scale" without justifying load):
    flag it, show the simpler tier that meets the stated needs and its lower cost/risk, and only
    build the complex version on explicit, informed human acknowledgment of the trade-off.
11. **You may not CLAIM an architectural property you did not VERIFY (§18).** "Modular monolith,"
    "no N+1," "right-sized," "scalable," "no cyclic dependencies" are *claims requiring evidence*
    (passing dependency-lint / fitness functions / load test / `EXPLAIN` / query-count check).
    State what you verified and how you verified it. An unverified architectural claim is a §16.9
    honesty violation. If the verification tooling (§18) is absent, say so and offer to add it
    rather than asserting the property.
12. **When asked to "continuously optimize," state the truth plainly:** you can optimize against
    the metrics PRESENT in this session/repo. You **cannot** observe production over time or
    remember it between sessions. Do not claim ongoing/continuous optimization you cannot
    perform. Instead, (a) optimize what the available evidence supports this session, and (b)
    propose/set up the observability (§11) and gates (§18) that let a human or scheduled job feed
    real metrics back in — that is the only honest mechanism for genuine continuous optimization.

---

## 17. Quality Checklist (run before calling an architecture "done")

```
REQUIREMENTS
[ ] §2 captured or explicitly estimated — load AND criticality, latency SLOs, consistency,
    team, budget, compliance, integration surface.
[ ] Chosen tier (§3) is the SIMPLEST that meets them — UNLESS criticality/compliance override
    raised the rigor deliberately. No unjustified complexity; no under-built high-stakes logic.

STRUCTURE
[ ] Clear layers/boundaries; domain free of framework/IO; dependencies point inward; no cycles.
[ ] Organized by feature/bounded-context; high cohesion, low coupling.
[ ] Boundaries ENFORCED by tooling (§18), not just claimed.

DATA
[ ] Relational-by-default unless a proven pattern needs otherwise; deliberate schema.
[ ] Queries indexed, paginated, bounded; no N+1 (verified); transactions for invariants
    (+ DB constraints for high-criticality invariants).
[ ] Migrations reversible & backward-compatible; backups automated + restore-tested; RPO/RTO set.
[ ] Consistency model (strong vs eventual) chosen deliberately per use case.

APIs & COMMUNICATION
[ ] Contracts versioned; explicit error schema + correlation id; pagination; idempotency keys.
[ ] External calls have timeouts + backoff retries + circuit breakers; consumers idempotent.
[ ] Async/queues for decoupling; sagas/outbox instead of distributed transactions.

PERFORMANCE & RESILIENCE
[ ] Stateless app tier; scaling levers chosen in order (do-less → vertical → horizontal → …).
[ ] Caching has explicit TTL/invalidation. Latency budget allocated to the tail (p99).
[ ] No single point of failure beyond SLO/criticality need; graceful degradation; health checks;
    graceful shutdown.

SECURITY (see SECURITY.md)
[ ] Zero-trust; authZ at the data layer; tenant isolation; secrets in a manager; TLS + at-rest encryption.

OBSERVABILITY & OPS
[ ] Structured logs + correlation ids; four golden signals; distributed tracing if >1 service.
[ ] SLOs defined; alerts on user-felt symptoms. IaC; CI/CD; safe deploys w/ auto-rollback; feature flags.
[ ] Platform right-sized (no Kubernetes-for-three-containers). Cost tracked.

VERIFICATION (§18)
[ ] Architectural claims are backed by passing tooling (dep-lint, fitness functions, load/EXPLAIN),
    not by narration. Missing gates are flagged as tracked debt.

DECISIONS
[ ] ADR written for every significant/irreversible decision (context, trade-offs, alternatives).

GUT CHECK (judgment — the human backstop)
[ ] Is this the SIMPLEST thing that meets the requirements — AND rigorous enough for the stakes?
[ ] Can a new engineer understand and operate this? Could the team actually run it?
[ ] What happens when each dependency fails — do I know, and is it acceptable?
```

---

## 18. Verification & Enforcement (doctrine doesn't enforce — these do)

Like SECURITY.md, this file is **doctrine**. An AI agent (or a human) can *claim* "modular
monolith," "no N+1," "right-sized," "no cycles" — and claims are worthless unverified. The
single most-claimed and least-achieved architecture in the industry is the "modular monolith,"
precisely because nothing enforces the boundaries. **Make the architecture machine-checkable so
output is graded by tools, not by its own narration.** If these gates are absent, that is itself
a tracked architectural gap — flag it and offer to add it (see §16.11).

### Boundary enforcement (turns "modular monolith" from a claim into a fact)
- Dependency-rule linting in CI: **dependency-cruiser** (JS/TS), **import-linter** (Python),
  **ArchUnit** (JVM), **go-arch-lint** (Go), or equivalent. **Fail the build** on: domain
  importing framework/IO, cross-context imports, dependency cycles. This is the entire
  difference between a modular monolith and a ball of mud — enforce it or you don't have one.

### Data & performance gates
- Query analysis in CI/staging: flag **N+1** (assert query-count-per-request budgets), **missing
  indexes** (`EXPLAIN` on slow queries), **unbounded queries** (no `LIMIT` on list endpoints).
- **Load/latency test on critical paths** in CI; fail on p99 SLO regression (§2).
- **Migration check:** forward-only, reversible, backward-compatible; block destructive ops
  without a tested rollback.

### Contract & dependency gates
- **API contract diff** (`openapi-diff` / `buf breaking`) — fail on breaking changes to a
  published contract without a version bump.
- **Architecture fitness functions** (Ford & Parsons): automated tests asserting your -ility
  properties, e.g. "no service reads another service's DB," "every external call is wrapped with
  a timeout," "domain package imports nothing from infrastructure."

### Resilience & observability gates
- Reject merges that add an external call without timeout + retry + correlation-id, or an
  endpoint without structured logging — these are lintable/testable patterns.

### Continuous optimization (the honest mechanism — see §16.12)
- "Continuous" optimization is **not** something a single agent session performs; it is a
  *standing loop*: §11 telemetry in production → SLO/error-budget alerts → periodic (human or
  scheduled) review feeding real metrics back into §15 Step 1. Set up the loop; don't claim the
  loop runs inside one conversation.

> Stack-specific tool choices and exact thresholds live in `ARCHITECTURE-STACK.md`. Keep tool
> selection there so this doctrine stays stack-agnostic and the tooling stays specific —
> the same split as `SECURITY-STACK.md` and `DESIGN-TOKENS.md`.

---

## References (the actual sources, not vibes)

- **Books:** *Designing Data-Intensive Applications* — Kleppmann (the single best systems book);
  *Software Architecture: The Hard Parts* & *Fundamentals of Software Architecture* —
  Richards & Ford (also the source of *fitness functions*); *Building Microservices* — Newman
  (incl. *when not to*); *Clean Architecture* — Martin; *Domain-Driven Design* — Evans;
  *Release It!* (resilience patterns) — Nygard; *Site Reliability Engineering* — Google;
  *Accelerate* — Forsgren/Humble/Kim (what actually correlates with performance: deploy
  frequency, lead time, MTTR, change-fail rate).
- **Essays/doctrine:** Fowler — *MonolithFirst*, *StranglerFigApplication*; Dan McKinley —
  *Choose Boring Technology*; Joel Spolsky — *Things You Should Never Do* (don't rewrite);
  Amazon — one-way/two-way doors; Google SRE — Four Golden Signals & error budgets.
- **Foundations:** CAP / PACELC theorems; NIST SP 800-207 (Zero Trust); the Twelve-Factor App;
  OpenTelemetry (observability); the AWS / Google / Azure Well-Architected / Cloud Architecture
  Frameworks (operational excellence, reliability, performance, cost, security pillars).
- **Inspiration ≠ imitation:** study *why* a pattern exists and *what scale/stakes it solves
  for*, then apply only if your §2 numbers (and criticality) call for it. Copying FAANG
  end-state diagrams without FAANG problems is the #1 way to build a slow, expensive, fragile
  system.

---

*The best architecture is the simplest one that meets the real requirements — load AND stakes —
is observable, fails gracefully, and is cheap to change. This document encodes the
decision-making so the result is right-sized (not the most impressive, the most appropriate),
and §18 makes that result verifiable rather than merely asserted. The document sets the process;
judgment executes it; the ADRs, the checklist, and the enforcement gates keep it honest.*
```

---
