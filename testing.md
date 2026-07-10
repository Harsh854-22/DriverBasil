# TESTING.md — Testing Strategy & Quality Standard

> **Status:** Mandatory testing standard for all engineers and AI agents in this repo.
> **Goal:** A test suite that is **fast, reliable, and trustworthy** — one that catches real
> regressions, runs in seconds-to-minutes, almost never flakes, and that engineers *trust enough
> to gate deploys on*. Tests that are slow, flaky, or brittle get ignored, then deleted, then
> the product breaks. A trusted suite is the product.
> **Priority:** Reliability (no flakes) > Speed > Meaningful coverage > Raw coverage number.
>
> **THE MOST IMPORTANT TRUTH IN THIS FILE (and the one most requests get wrong):**
> The goal is **fewer, better tests — not more tests, not 100% coverage.** Elite practice
> (*Software Engineering at Google*, the Test Pyramid, Microsoft/Amazon engineering) is to
> have **mostly small, fast, isolated tests**, far fewer integration tests, and very few
> end-to-end tests. **Test behavior, not implementation.** **100% coverage is an anti-goal** —
> it rewards testing trivia and produces brittle tests coupled to internals. Knowing *what NOT
> to test* is as important as what to test. A 10/10 suite is *trustworthy and fast*, not *huge*.
>
> **WHAT TESTS DO AND DON'T DO:** Unit/integration/E2E tests prevent *logic* regressions. They
> do **not**, by themselves, prevent *scale* failures — only **load/performance/soak/chaos
> testing against real infrastructure + production observability** (§9, §10, ARCHITECTURE.md
> §11) catch those. This doc specifies both; the scale parts must actually be *run* against real
> environments — no document executes them for you.

---

## 0. How to Use This File

- **New project:** establish the §3 pyramid shape, the §2 philosophy, and CI gates (§11) from
  day one. Start with unit + a thin layer of integration + 1–3 critical E2E flows. Grow upward
  only as risk justifies.
- **Existing project (the honest "fix a bad/untested codebase" path):** follow §12 — **do NOT
  try to retroactively reach 100% coverage.** Add tests where risk and change concentrate
  (characterization tests + test-the-bug-you're-fixing), stabilize flakes first, build upward.
- **Per-stack specifics** (exact frameworks, runners, mocking libs, thresholds, browser tooling)
  live in **`TESTING-STACK.md`** — fill once per project. Missing = tracked gap (§13).
- **AI agents:** §13 is binding. Core rules: write *meaningful* tests not coverage-padding;
  never claim a test/check ran unless it actually executed; never fake a green result.

---

## 1. First Principles

1. **A test exists to give you confidence to change code.** If a test doesn't increase real
   confidence (or would only break on a legitimate refactor), it's a liability, not an asset.
2. **Test behavior (the public contract), not implementation (internals).** Tests coupled to
   *how* code works break on every refactor and train engineers to ignore failures. Tests
   coupled to *what* code does survive refactors and catch real bugs. This is the single most
   important rule for a maintainable suite.
3. **Reliability beats everything. A flaky test is worse than no test** — it erodes trust in the
   *entire* suite, gets `@skip`ped, and the next real failure is ignored. Zero tolerance for
   flakes (§7).
4. **Fast feedback is the point.** The unit suite should run in seconds. Slow tests don't get
   run, so they don't catch anything. Speed is a feature.
5. **The Test Pyramid (Cohn), not the ice-cream cone.** Many fast unit tests → fewer integration
   tests → very few E2E tests. Inverting it (mostly E2E) gives slow, flaky, expensive suites.
6. **Tests are first-class code.** Same naming, clarity, and review standards (CONVENTIONS.md).
   But: tests prefer *readability and obviousness* over DRY — a little duplication in tests is
   fine if it makes the test readable in isolation.
7. **Coverage is a flashlight, not a goal.** Use it to find *untested risk*, never as a target
   to hit. Chasing a number produces tests of getters and trivia. (§8)
8. **Tests should fail for exactly one reason, and the failure should tell you what broke** — a
   clear name + a clear assertion = a test you can debug in seconds, not minutes.

---

## 2. What to Test — and What NOT to Test (the skill most miss)

### Test (high value)
- **Business logic, rules, calculations, state transitions** — the core domain (your money's worth).
- **Edge cases & boundaries:** empty, null/None, zero, negative, max, off-by-one, the boundary
  *exactly* and *±1*. Bugs cluster at boundaries.
- **Error paths and failure modes** — what happens when input is bad, a dependency fails, a
  timeout fires. (Most bugs hide here; most weak suites test only the happy path.)
- **Bug regressions:** every fixed bug gets a test that fails on the old code and passes on the
  fix — so it never comes back. (Non-negotiable.)
- **Contracts/boundaries:** API request/response shapes, schema validation, public interfaces.
- **Security-critical paths:** authn/authz, ownership/IDOR, input validation (cross-ref
  SECURITY.md §13 minimum security tests).
- **Concurrency/idempotency** where the design relies on it (ARCHITECTURE.md §7).

### Do NOT test (low/negative value — testing these is a defect)
- **Third-party libraries / the framework / the language.** Trust them; test *your* use of them.
- **Trivial code with no logic:** plain getters/setters, pass-through wrappers, simple DTOs,
  constants, auto-generated code.
- **Implementation details:** private methods directly, internal call counts, "was this exact
  helper invoked" — test the *observable behavior* instead.
- **Configuration/glue with no branching.**
- **The happy path *only*** — that's not "tested," that's the easy 10% (DESIGN.md §10 / §2 here).
- **Things better caught by the type checker, linter, or formatter** — don't write a test for
  what `tsc --strict`/`mypy`/the compiler already proves. Push correctness left to the type
  system (CONVENTIONS.md §13).

> **The judgment:** test the code where a bug would *hurt* and where logic is *non-obvious*. Skip
> the code where a bug is impossible or trivial. Coverage of *risk*, not coverage of *lines*.

---

## 3. The Test Pyramid (the shape of a healthy suite)

Most tests at the bottom (fast, cheap, isolated); fewest at the top (slow, expensive, realistic).

```
        ▲  E2E / UI / Browser  ← very few; only critical user journeys (slow, flaky-prone)
       ───  Integration         ← some; real DB/queue/service-to-service, contract tests
      ─────  Unit               ← many; pure logic, milliseconds, no I/O  (the foundation)
     ───────
   (+ a slice of) Static analysis: types, lint, format — the cheapest "tests" of all, run first.
```

### Unit tests (the foundation — most of your tests)
- Test one unit of behavior in isolation; **no real I/O** (no network/DB/filesystem/clock/random
  — inject or fake them). Run in **milliseconds**.
- Fast enough to run on every save. This layer is where you cover edge cases exhaustively.

### Integration tests (fewer — verify the seams)
- Test components working together with **real adjacent infrastructure** (real DB, real queue) —
  use ephemeral instances (e.g. Testcontainers / an in-memory or disposable DB), **not mocks of
  your own datastore.** Mocking your own DB then asserting on the mock tests nothing real.
- Cover: data-layer queries (against a real DB), API endpoint round-trips, service-to-service
  contracts, migrations apply cleanly.
- **Contract tests** (consumer-driven, e.g. Pact) where independent services/teams integrate —
  so a provider change can't silently break a consumer (ARCHITECTURE.md §6).

### End-to-End tests (very few — only the journeys that *must* work)
- Drive the whole system as a user would (API or browser). Reserve for **critical revenue/safety
  paths**: sign-up → login, checkout/payment, the core "job to be done." A handful, not hundreds.
- E2E is the *slowest and flakiest* layer — keep it tiny and rock-solid, or it poisons trust.
- (Browser-driven E2E: see §6.)

> **Anti-pattern — the "ice-cream cone":** mostly E2E, few unit. It's slow, flaky, expensive to
> maintain, and gives slow feedback. If you find yourself there, push tests *down* the pyramid.

---

## 4. The Anatomy of a Good Test

- **Arrange–Act–Assert** (a.k.a. Given–When–Then). Visible three-part structure.
- **Name = a specification of behavior.** `it("rejects checkout when the cart is empty")`, not
  `test1` / `testCheckout`. The name should read as a sentence about what the system does.
- **One logical behavior per test.** Multiple physical asserts are fine if they verify *one*
  behavior; don't cram unrelated behaviors into one test (a failure should point to one cause).
- **Deterministic.** Same input → same result, every run, every machine, any order. No reliance
  on time-of-day, timezone, locale, random seeds, network, or test execution order. Control the
  clock and randomness by injection (§7).
- **Independent & isolated.** Tests don't share mutable state or depend on each other; each
  sets up and tears down its own world. Parallel-safe.
- **Realistic, meaningful data.** Use builders/factories/fixtures for domain objects; name data
  to express intent (`expiredCard`, `emptyCart`), not `foo`/`bar` (CONVENTIONS.md §10).
- **Tests the contract, survives refactors.** If you can rewrite the implementation and the test
  still passes (because behavior is unchanged), it's a good test. If a pure refactor breaks it,
  it was testing implementation — fix the test, not just the code.
- **Fast.** A unit test that takes >100ms is suspect — it's probably doing real I/O it shouldn't.

---

## 5. Test Doubles (mocks/stubs/fakes — use deliberately, not reflexively)

Over-mocking is a top cause of brittle, false-confidence suites (you end up testing the mock).

- **Prefer real over fake over mock**, in that order, for *your own* code: use the real thing
  if it's fast and deterministic; a lightweight **fake** (in-memory implementation) if not; a
  **mock** only to verify an interaction at a true boundary.
- **Mock at architectural boundaries only** (third-party APIs, payment gateways, email/SMS,
  external services) — the things you don't own and that are slow/nondeterministic/costly.
- **Do NOT mock your own database** in integration tests — use a real ephemeral DB
  (Testcontainers). Mocking the DB and asserting on the mock proves nothing about real queries.
- **Don't assert on mock internals** ("was method X called 3 times") unless the *interaction
  itself* is the contract — that's testing implementation (§1.2).
- **Stub external time/randomness/UUIDs** so tests are deterministic (§4).
- **Fakes you own** (in-memory repo) often beat mocks: they're reusable, behave like the real
  thing, and don't couple every test to call signatures.

---

## 6. Browser & UI Testing (incl. AI-agent-driven testing)

For web UIs, the top of the pyramid is browser E2E — kept small (§3) and stable.

### How to do browser tests well
- **Use a modern browser-automation framework** (Playwright preferred; Cypress) — record exact
  tool in `TESTING-STACK.md`. They give auto-waiting, network interception, tracing, video.
- **Select by user-visible, stable locators** — accessible role/label/text or explicit
  `data-testid` — **never** by brittle CSS/XPath that breaks on restyling (DESIGN.md changes
  shouldn't break tests).
- **No fixed `sleep()`s.** Wait on conditions/elements/network, not arbitrary time — fixed waits
  are the #1 source of browser-test flake (§7).
- **Test critical journeys only** (sign-up, login, checkout) — and the must-work states.
- **Cover the states DESIGN.md §10 demands:** loading, empty, error, success — not just happy path.
- **Cross-cutting browser checks worth automating:** accessibility (axe-core: contrast, roles,
  keyboard — DESIGN.md §11), responsive/viewport sanity at key breakpoints, and visual-regression
  snapshots for pages where look matters (with sane tolerances to avoid flake).
- **Run headless in CI; keep traces/video on failure** for debugging.

### AI-agent-driven browser testing (when the agent has browser access)
If the agent can drive a browser (Playwright MCP/automation, or computer-use):
- **Prefer writing/running a Playwright (or stack-standard) E2E test** over ad-hoc clicking —
  produce a *repeatable, committed* test, not a one-off manual run. A repeatable test is an
  asset; a one-time manual click is not.
- The agent **may** use the browser to: reproduce a bug, verify a fix end-to-end, explore real
  rendered states, check the DESIGN.md §10 states actually render, and run an a11y scan.
- **Honesty (binding, §13):** the agent must distinguish *"I wrote and ran an automated browser
  test, here's the result/trace"* from *"I manually navigated and observed."* It must **never
  claim a browser/E2E test passed unless it actually executed and observed the result.** A
  described-but-unrun test is not a passed test.
- **If no browser access is available,** the agent says so plainly and writes the E2E test for a
  human/CI to run — it does **not** pretend to have executed it.

---

## 7. Flakiness — Zero Tolerance (a flaky test poisons the whole suite)

A flaky test (passes/fails without code change) is treated as a **bug to fix or quarantine
immediately**, because it trains everyone to ignore red. Common causes + fixes:

- **Timing/race** → never `sleep()`; wait on explicit conditions; control concurrency.
- **Time/timezone/locale** → inject a fixed clock; pin timezone/locale in tests.
- **Randomness** → seed it or inject deterministic values.
- **Test order / shared state** → full isolation; fresh state per test; parallel-safe by design.
- **External network** → mock/fake the external boundary; never hit the real internet in tests.
- **Resource cleanup** → tear down everything; no leaked rows/files/connections between tests.

**Policy:** a flaky test is **quarantined (skipped *with a tracked ticket*) immediately** so it
stops blocking, then **fixed or deleted within a bounded time** — never left flaking in the gate.
Track a flake rate; a rising flake rate is a quality emergency.

---

## 8. Coverage — Use It Right (it's a flashlight, never a target)

- **Coverage tells you what is *not* tested. It does NOT tell you what is tested *well*.** 100%
  line coverage can still miss every meaningful bug (you can execute a line without asserting on
  its outcome).
- **Set a *floor*, not a *target*** — and a *meaningful* one (e.g. high coverage on core domain
  logic; don't demand it on glue/config/generated code). A blanket "100% everywhere" mandate
  produces brittle trivia-tests and is an explicit anti-goal (§ intro, §1.7, §2).
- **Prefer branch/path coverage over line coverage** for logic-heavy code.
- **Use coverage diffs in PRs** ("did this change leave new logic untested?") rather than chasing
  a global number.
- **Mutation testing** (Stryker/PIT/mutmut, if the stack supports it) is a far better measure of
  *test quality* than coverage — it checks whether your tests would actually *catch* a bug by
  mutating code and seeing if a test fails. Use it on critical modules to find weak tests.

---

## 9. Beyond Correctness — Testing for Scale & Resilience (so it doesn't break in prod)

These are what actually keep a system from breaking at scale. **They must run against realistic
infrastructure — a document specifies them; it cannot execute them.** Match the rigor to
ARCHITECTURE.md §2 (load *and* criticality).

- **Performance / micro-benchmarks:** benchmark hot paths; guard against perf regressions in CI
  where feasible (fail if a critical path's latency/allocations regress).
- **Load testing:** simulate expected *and* peak traffic (k6, Gatling, Locust, JMeter) against a
  prod-like environment; verify the p95/p99 SLOs from ARCHITECTURE.md §2 hold under load.
- **Stress testing:** push *past* expected peak to find the breaking point and confirm it fails
  gracefully (load-sheds/degrades — ARCHITECTURE.md §8/§9), not catastrophically.
- **Soak/endurance testing:** sustained load over hours/days to surface memory leaks, connection
  exhaustion, and slow degradation that short tests miss.
- **Spike testing:** sudden traffic jumps (verify autoscaling/backpressure behave).
- **Chaos/resilience testing** (higher tiers): inject failures — kill instances, add latency,
  drop a dependency — and verify timeouts/retries/circuit-breakers/fallbacks work as designed
  (ARCHITECTURE.md §9; *Release It!*; Netflix Chaos Engineering). Practice failure before it
  practices on you.
- **The honest truth (§ intro):** unit/integration/E2E catch *logic* regressions; **only the
  tests in this section + production observability (ARCHITECTURE.md §11) prevent *scale*
  failures.** A green unit suite says nothing about behavior at 10,000 req/s.

---

## 10. Specialized & Cross-Cutting Testing (apply when relevant)

- **Security testing:** the SECURITY.md §13 minimum set is mandatory where applicable —
  unauth→401, cross-user object access (IDOR)→403, over-limit→429, injection payloads rejected.
  Plus SAST/dependency-scan in CI (SECURITY.md §13). Don't duplicate; *reference* SECURITY.md.
- **Accessibility testing:** automated a11y (axe-core) on key pages + the DESIGN.md §11 manual
  checks (keyboard, focus, screen-reader spot-checks) — automation catches ~30–50%, the rest is
  manual.
- **Property-based testing** (QuickCheck/Hypothesis/fast-check): for pure logic with many inputs
  (parsers, serializers, math, validators), assert *properties* hold across generated inputs —
  finds edge cases humans don't imagine. High value for algorithmic code.
- **Snapshot testing:** use sparingly and *review* snapshots — blindly-updated snapshots test
  nothing. Good for serialized output/config; risky for large UI snapshots (brittle).
- **Smoke tests:** a tiny set of "is it fundamentally alive" checks to run post-deploy against
  prod/staging (does it boot, can it serve the health check + one core path).
- **Contract tests:** (§3) between independently-deployed services.
- **Mutation tests:** (§8) to validate the tests themselves on critical code.

---

## 11. CI/CD Integration — Tests as the Gate (where testing actually enforces quality)

Same principle as the other docs: **the suite only protects you if it gates the merge.** Tests
that don't block bad code from shipping are decoration.

- **Run order = fast-fail (cheapest first):** format-check → lint → typecheck → unit →
  integration → (E2E on critical paths) → security/dependency scan. Stop at first failure.
- **Block merge on any failure.** Branch protection requires the suite green (cross-ref
  SECURITY.md §13, ARCHITECTURE.md §18, AGENTS.md verification).
- **Speed: keep the gating suite fast** (parallelize, shard, cache deps, run only affected tests
  where the toolchain supports it). A slow CI gate is bypassed or ignored — defeating the point.
- **Tiered runs:** fast unit+integration on every push/PR; heavier suites (full E2E, load,
  mutation, soak) on a schedule / pre-release / nightly — not blocking every commit.
- **Post-deploy smoke tests** against the deployed environment, with **automatic rollback** on
  failure (ARCHITECTURE.md §12).
- **Flake tracking:** quarantine lane + flake-rate metric (§7); flaky tests never silently
  degrade the gate.
- **Coverage diff** reported on PRs (§8), not enforced as a hard global number.

---

## 12. Existing-Project Playbook (the honest "add testing to a bad/untested codebase" path)

You **cannot** "upload a doc and auto-test" an untested codebase to 10/10 — and you must **never
try to retroactively hit high coverage everywhere** (that produces weeks of brittle trivia-tests
and ships nothing). Do this instead (the senior approach, *Working Effectively with Legacy Code*,
Feathers):

1. **Stabilize the gate first.** Get *something* green and fast in CI (lint+typecheck+format if
   no tests exist). Quarantine any existing flakes (§7) so the signal is trustworthy.
2. **Add a thin layer of high-value E2E/smoke** on the 1–3 *most critical* user journeys — a
   safety net that catches catastrophic breakage while you work, even before unit coverage exists.
3. **Test the bug you're fixing.** Every bug fix from now on ships with a regression test (§2).
   This is the highest-ROI rule and compounds over time.
4. **Test where you change + where risk concentrates.** Add tests to code you're about to
   modify (characterization tests: pin current behavior first, *then* refactor safely). Don't
   chase untouched, low-risk code.
5. **Find seams to break dependencies.** Legacy code is hard to test because it's tightly
   coupled; introduce interfaces/injection at boundaries (CONVENTIONS.md §3, ARCHITECTURE.md §4)
   so units become testable — incrementally, never a big-bang rewrite (ARCHITECTURE.md §15).
6. **Build the pyramid upward over time**, prioritizing by risk × change-frequency. Stop when
   the suite is *trustworthy and fast* — not when a coverage number is hit.

> A small, fast, trusted suite that covers the critical paths beats a giant brittle one you
> built chasing 100% and now everyone ignores.

---

## 13. Instructions for AI Agents (binding)

If you write, modify, run, or review tests here, this binds you.

1. **Write *meaningful* tests, not coverage padding.** Test behavior/contracts and the risky/
   edge/error paths (§2). Do **not** write tests for trivial getters, the framework, third-party
   libs, or implementation details. Do **not** chase 100% coverage — it's an anti-goal (§ intro,
   §8). Knowing what *not* to test is part of the job.
2. **Follow the pyramid (§3):** default to fast isolated unit tests; add integration tests
   against a *real ephemeral* DB/queue (not a mock of your own datastore — §5); add E2E only for
   critical journeys.
3. **Every test must be deterministic and isolated (§4, §7).** Inject clock/randomness/UUIDs;
   never `sleep()`; never hit the real network; no cross-test shared state. A flaky test is a
   defect — don't author one.
4. **Test the bug you fix.** Any bug fix includes a regression test that fails on the old code
   and passes on the fix (§2, §12.3). No exceptions.
5. **Mock only at true boundaries (§5);** prefer real/fake over mock for your own code; don't
   assert on mock internals.
6. **Read `TESTING-STACK.md`** for the project's frameworks, runners, mocking approach, coverage
   floors, and browser tooling. Match the existing tests' style/structure (CONVENTIONS.md §1.1)
   before inventing your own.
7. **Browser/E2E honesty (§6):** if you have browser access, prefer writing a *repeatable*
   Playwright/stack-standard test over manual clicking, and run it. **Never claim a browser or
   E2E test passed unless it actually executed and you observed the result.** If you lack browser
   access, say so and write the test for CI/a human — do not pretend to have run it.
8. **General honesty (shared across all docs — the cardinal rule):** **never report a test/check
   as passing unless you actually ran it and saw it pass.** State exactly which suites you ran
   (unit/integration/E2E/etc.) and which you did not. A described test is not a run test; a green
   you didn't see is a lie. (Mirrors AGENTS.md verification, SECURITY/ARCHITECTURE/CONVENTIONS
   honesty rules.)
9. **Scale testing is specified here but you cannot execute it without real infra (§9).** When
   asked to "make sure it won't break at scale," say plainly: unit/integration/E2E don't prove
   scale; propose/author the load/stress/soak/chaos tests (§9) and the observability
   (ARCHITECTURE.md §11), and state that they must be *run against a prod-like environment* — do
   not claim scale-safety you didn't measure.
10. **For an existing untested codebase, follow §12** — stabilize, thin E2E safety net,
    test-the-bug, test-where-you-change. **Never propose a from-scratch retroactive
    100%-coverage effort** as the default.
11. **If the CI test gate (§11) is missing,** flag it and offer to add it — the suite only
    protects the codebase if it blocks merges (§11).

---

## 14. Quality Checklist (run before calling testing "done")

```
STRATEGY & SHAPE
[ ] Pyramid shape: mostly fast unit, fewer integration, very few E2E (not an ice-cream cone).
[ ] Tested the risky/edge/error paths — not just the happy path; skipped trivia/framework/3rd-party.
[ ] Every bug fix includes a regression test.

TEST QUALITY
[ ] Tests verify behavior/contracts, survive a pure refactor (not coupled to implementation).
[ ] AAA structure; names read as behavior specs; one behavior per test; clear failure messages.
[ ] Deterministic (clock/random injected, no sleeps, no real network); isolated; parallel-safe.
[ ] Realistic, intent-named test data; mocks only at true boundaries; real ephemeral DB for integration.

RELIABILITY & SPEED
[ ] No known flaky tests in the gate (flakes quarantined + ticketed); flake rate tracked.
[ ] Unit suite runs in seconds; gating CI suite is fast (parallel/sharded/affected-only).

COVERAGE (flashlight, not target)
[ ] Meaningful coverage on core logic (floor, not 100% mandate); coverage diff reviewed on PR.
[ ] (Critical modules) mutation testing or property tests validate the tests actually catch bugs.

CROSS-CUTTING (where applicable)
[ ] Security min-set (SECURITY.md §13: 401/403-IDOR/429/injection) present.
[ ] A11y automated scan + manual checks on key pages (DESIGN.md §11).
[ ] Browser E2E on critical journeys covers loading/empty/error/success states (DESIGN.md §10).

SCALE & RESILIENCE (match ARCHITECTURE.md §2 load + criticality — and actually RUN against prod-like infra)
[ ] Load test verifies p95/p99 SLOs under expected + peak traffic.
[ ] Stress test confirms graceful degradation past peak (not catastrophic failure).
[ ] Soak test for leaks/exhaustion; chaos test for timeout/retry/breaker/fallback (higher tiers).

CI GATE
[ ] Suite gates the merge (block on failure); post-deploy smoke + auto-rollback; honest run-reporting.

GUT CHECK (the human/reviewer backstop)
[ ] Do I trust this suite enough to deploy on green? If a test fails, do I believe something is actually broken?
[ ] If I refactored the implementation, would these tests still pass (good) or break (bad)?
[ ] Did I write fewer-better tests, or did I pad coverage with trivia?
```

---

## References (the actual sources, not vibes)

- **Books:** *Software Engineering at Google* — Winters/Manshreck/Wright (the definitive
  large-scale testing philosophy: small/medium/large tests, "fewer better tests," flakiness as a
  systemic threat); *Working Effectively with Legacy Code* — Feathers (seams, characterization
  tests — the §12 playbook); *xUnit Test Patterns* — Meszaros (test smells, doubles, the
  vocabulary of good tests); *Release It!* — Nygard (resilience/stability testing — §9); *Growing
  Object-Oriented Software, Guided by Tests* — Freeman & Pryce; *The Art of Software Testing* —
  Myers.
- **Foundations/essays:** Mike Cohn — the **Test Pyramid**; Martin Fowler — *TestPyramid*,
  *UnitTest*, *TestDouble*, *Eradicating Non-Determinism in Tests* (flakiness); the **Google
  Testing Blog** + "Testing on the Toilet" (Small/Medium/Large test sizing, flaky-test policy);
  Netflix — **Chaos Engineering** / *Principles of Chaos*.
- **Techniques:** Property-based testing (QuickCheck → Hypothesis/fast-check); Mutation testing
  (Stryker/PIT/mutmut); Consumer-driven contract testing (Pact); k6/Gatling/Locust/JMeter (load).
- **Tooling lives in `TESTING-STACK.md`:** the specific runner, assertion lib, mocking lib,
  browser tool (Playwright/Cypress), coverage tool, load tool, and thresholds for THIS stack.
- **The discipline ≠ dogma:** the goal is a *fast, trusted suite that catches real regressions
  and proves the system holds under its real load* — not the most tests, not 100% coverage, not
  every technique applied everywhere. Right-sized testing, like right-sized architecture.

---

*A test suite's only job is to let you change and ship code with confidence. The best suite is
fast, reliable, and trustworthy — mostly small tests, behavior not implementation, zero flakes,
gating the merge — and backed by load/chaos testing + observability for the scale it can't prove
on its own. This document encodes the strategy; the CI gate (§11) makes it enforced; the
load/chaos tests (§9) must be run against real infra; and the honesty rules (§13) keep every
green result real. Fewer, better tests — not more.*
```

---
