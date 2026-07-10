# ENGINEERING_STANDARDS.md — Ways of Working & Process Standard

> **Status:** Mandatory engineering process standard for all engineers and AI agents.
> **Goal:** A consistent, high-quality *workflow* — how code is structured, branched, reviewed,
> and shipped — so that work from any author (human or any AI model) flows the same way and
> meets the same bar before it lands.
> **Priority:** Working software in main > consistent process > individual preference.
>
> **WHAT THIS FILE IS (and is NOT):** This is the **connective tissue / operating manual** — the
> "how we work" that no single deep doc owns: PR rules, git workflow, folder defaults, Definition
> of Done, review standards, file-size/PR-size limits. It **points to** the deep standards rather
> than restating them, to avoid drift:
> - *What good code looks like* → **CONVENTIONS.md** (+ GLOSSARY.md)
> - *Architecture & folder boundaries* → **ARCHITECTURE.md** (esp. §4)
> - *Security rules* → **SECURITY.md**
> - *Design/UX* → **DESIGN.md**
> - *Testing strategy* → **TESTING.md**
> - *Agent workflow/verification* → **AGENTS.md**
> If a rule lives in one of those, this file links to it — it does not repeat it. When this file
> and a deep doc seem to conflict, the **deep doc wins** on its own topic.

---

## 0. How to Use This File

- **New engineer/agent:** read this to learn *how work flows here* (branch → build → review →
  merge → ship). Read the deep docs (above) to learn *what the bar is* per topic.
- **Per-project specifics** (exact branch names, CI provider, PR template path, repo's actual
  folder tree) live in **`ENGINEERING-STACK.md`** or the repo's `CONTRIBUTING.md` — fill once.
- **AI agents:** §10 is binding. Core rules: respect the Definition of Done (§2), keep
  changes small and reviewable (§4), follow the deep docs for topic rules, never claim a gate
  passed you didn't run.

---

## 1. Engineering Values (the "why" behind the process)

1. **Main is always shippable.** `main`/`trunk` is never broken. If it breaks, fixing it is the
   team's top priority (stop-the-line). Everything below serves this.
2. **Small changes, fast feedback.** Small PRs review faster, break less, revert cleanly, and
   ship sooner. Big-bang changes are a process smell (§4).
3. **Optimize for the reader and the reviewer.** Code is read far more than written
   (CONVENTIONS.md §1.4); a PR is read by a reviewer who wasn't in your head — make both easy.
4. **Automate the boring rules; reserve humans for judgment.** Formatting/lint/types/tests are
   machine-checked (the deep docs' enforcement sections); review focuses on *design, correctness,
   and intent*, not style nits.
5. **Reversibility & safety.** Prefer changes that are easy to roll back; gate risky changes
   harder (AGENTS.md SAFE MODE, ARCHITECTURE.md §1.8 Type-1/2 decisions).
6. **Leave it better (Boy Scout Rule) — within scope.** Improve what you touch; don't sprawl an
   unrelated refactor into a feature PR (§4 — keep diffs focused).

---

## 2. Definition of Done (the universal completion contract)

A change is **DONE** only when ALL of these hold. This is the single most important section —
it's the shared bar that makes "done" mean the same thing for every author.

```
[ ] Builds with no errors.
[ ] Formatter run; linter clean; types pass (strict).            → CONVENTIONS.md §11–13
[ ] Tests written for new logic + bug regressions; suite green.  → TESTING.md §2, §13
[ ] Follows the codebase's existing patterns & naming.           → CONVENTIONS.md §1, GLOSSARY.md
[ ] Architecture boundaries respected; no new cycles.            → ARCHITECTURE.md §4, §18
[ ] Security rules satisfied for touched code.                   → SECURITY.md §2, §12
[ ] UI states (loading/empty/error/success) handled, if UI.      → DESIGN.md §10
[ ] No dead code, commented-out code, debug prints, or stray TODOs without owners.
[ ] Docs updated if behavior/API/setup changed (incl. GLOSSARY for new domain terms).
[ ] No secrets, keys, or PII added anywhere (code/config/logs).  → SECURITY.md §2
[ ] The diff is focused — no unrelated changes or reformatting churn.
[ ] All claimed checks were ACTUALLY run (no faked greens).      → AGENTS.md verification
```

> "Done" ≠ "the happy path works on my machine." Done = the above, verified.

---

## 3. Repository & Folder Structure

The deep ownership of structure is **ARCHITECTURE.md §4** (layering, boundaries, dependency
rules, feature/context grouping). This section adds the **practical repo defaults** that doc
doesn't specify.

- **Group by feature/domain once the project grows** (ARCHITECTURE.md §4), not by technical type.
  Small projects may stay flat — be consistent and record the actual tree in
  `ENGINEERING-STACK.md`.
- **A sane default top-level layout** (adapt per stack; record real one per project):
  ```
  /src            → application source (organized per ARCHITECTURE.md §4)
  /tests          → tests (or co-located *.test.* per TESTING-STACK.md — pick one, be consistent)
  /docs           → docs, incl. /docs/decisions for ADRs (ARCHITECTURE.md §14)
  /scripts        → dev/ops scripts (setup, migrate, seed)
  /config         → config & env templates (.env.example — never real secrets, SECURITY.md §3)
  /.github        → CI workflows, PR/issue templates, CODEOWNERS
  README.md       → what it is, how to run, how to test, how to contribute (links to these docs)
  ```
- **The standard doc set lives at the repo root or `/docs`:** AGENTS.md, ARCHITECTURE.md,
  SECURITY.md, DESIGN.md, CONVENTIONS.md, TESTING.md, GLOSSARY.md, this file, and their
  `*-STACK.md` companions.
- **One canonical place per thing.** Don't scatter config, don't duplicate utilities, don't have
  two folders that do the same job. New contributors should *predict* where something lives.
- **Generated/vendored dirs are off-limits to hand-edits** and gitignored or clearly marked
  (AGENTS.md repo map): `node_modules/`, `dist/`, `build/`, `*.gen.*`, lockfiles are committed
  but not hand-edited.
- **README is the front door:** how to install, run, test, and contribute, plus links to this
  doc set. A new engineer should be productive from the README alone.

---

## 4. Change Size & Scope (small, focused, reviewable)

- **One logical change per PR.** A PR does one thing: a feature, a fix, or a refactor — not all
  three. Mixed PRs are hard to review and risky to revert.
- **Keep PRs small.** Target a reviewable size (rough guide: a few hundred lines of *meaningful*
  diff; large generated/lockfile changes don't count). Big features → split into stacked/
  incremental PRs behind a feature flag (ARCHITECTURE.md §12).
- **Separate refactors from behavior changes.** A pure refactor PR should have *no* behavior
  change (and tests prove it — TESTING.md §4 "survives refactor"). A behavior PR shouldn't smuggle
  in a big refactor. This keeps diffs honest and reviewable.
- **No unrelated reformatting in a feature diff** (CONVENTIONS.md §12) — it hides the real change.
- **File-size smell:** an overlong file usually means it's doing too much — split by
  responsibility, not arbitrary line count (CONVENTIONS.md §4).

---

## 5. Git & Branching Workflow

(VCS *behavior for agents* — when to commit/push — is governed by AGENTS.md. This is the *team
workflow* and *format*. Record the project's exact scheme in `ENGINEERING-STACK.md`.)

- **Trunk-based or short-lived feature branches** off `main`. Branches are short-lived (hours to
  a few days) — long-lived branches cause painful merges and drift. Prefer small PRs merged
  frequently over big branches living for weeks.
- **Branch naming:** consistent scheme, e.g. `type/short-description` (`feat/cart-checkout`,
  `fix/login-401`, `chore/bump-deps`). Record the repo's convention.
- **Commits:** atomic, imperative subject, Conventional Commits if the repo uses them
  (CONVENTIONS.md §12). One logical change per commit; don't mix concerns.
- **Rebase/squash policy:** follow the repo's convention (commonly: squash-merge to keep `main`
  history clean, or rebase to keep it linear) — record it; be consistent.
- **`main` is protected** (§7): no direct pushes; merge only via reviewed, green PR.
- **Never force-push shared branches.** Never rewrite published history others build on.

---

## 6. Pull Request Standard

A PR is a unit of communication, not just a code dump. Every PR includes:

### PR description must answer
```
WHAT:  one-line summary of the change.
WHY:   the problem/motivation (link the issue/ticket).
HOW:   key approach / notable decisions / trade-offs (link an ADR if architectural — §14 ARCH).
RISK:  low | med | high (high = touches auth/data/payments/migrations/public API → SAFE MODE).
TESTING: what tests were added; what you actually ran and verified (no faked greens — §2).
SCREENSHOTS/RECORDING: for any UI change (before/after; the DESIGN.md §10 states).
ROLLBACK: how to revert if it goes wrong (esp. for med/high risk).
```

### PR rules
- **Small and focused** (§4). One logical change. Reviewable in one sitting.
- **Self-review first.** Read your own diff before requesting review — catch the obvious.
- **CI must be green** before requesting review (don't make humans review broken code) — §7.
- **The author owns getting it merged:** respond to review, keep it rebased, don't let it rot.
- **Link the work:** issue/ticket, ADR (if architectural), related PRs.
- **A PR template** (`.github/pull_request_template.md`) encodes the above so it's automatic.

---

## 7. Code Review Standard

Review is where quality and knowledge-sharing happen. The bar and the etiquette:

### What reviewers check (in priority order)
1. **Correctness:** does it do what it claims? edge cases & error paths handled? (TESTING.md §2)
2. **Design/architecture:** right approach? boundaries respected? not over/under-engineered?
   (ARCHITECTURE.md §13)
3. **Security:** the SECURITY.md §12 checklist for any code touching auth/data/input/secrets.
4. **Tests:** meaningful (behavior, not trivia), present for new logic + the fixed bug
   (TESTING.md §13). Not coverage-padding.
5. **Readability/consistency:** matches existing patterns & naming (CONVENTIONS.md §1, GLOSSARY).
6. **Docs:** updated if needed (§2).
   > **Not the reviewer's job:** style/format nits — those are the formatter's and linter's job
   > (CONVENTIONS.md §11–13). If a human is arguing about spaces, the tooling is missing.

### Review etiquette (process quality, not just code quality)
- **Review promptly** — a stale PR blocks the author and rots. Treat review as real work.
- **Critique the code, not the person.** Comments are kind, specific, and explain *why*.
- **Distinguish blocking from non-blocking** — mark "must fix" vs. "nit/optional/suggestion"
  (e.g. prefix `nit:`). Don't block a PR on personal preference.
- **Ask, don't command,** when intent is unclear ("what happens if X is null?").
- **Approve when it's *better than the current state and meets the bar* — not when it's perfect.**
  Perfect-blocking stalls delivery; "good and safe" ships.
- **Required approvals + CODEOWNERS** on critical paths (auth, crypto, migrations, CI, this doc
  set) — SECURITY.md §13, ARCHITECTURE.md §18.
- **Branch protection:** green CI + required review before merge. No exceptions for "small" changes.

---

## 8. Quality Gates & Automation (process side)

The deep docs each own their enforcement (CONVENTIONS.md §13, SECURITY.md §13, ARCHITECTURE.md
§18, TESTING.md §11). This section is the **unified pipeline order** that ties them together —
the single "wall" every change passes through:

```
LOCAL (pre-commit hook):   format → lint → typecheck → fast unit tests
PR (CI, fast-fail order):  format-check → lint → typecheck → unit → integration
                           → security scan (SAST + deps) → E2E (critical paths)
                           → coverage diff report
MERGE GATE:                all green + required review(s) + CODEOWNERS where applicable
POST-MERGE / DEPLOY:       build artifact → deploy (canary/blue-green) → smoke tests
                           → auto-rollback on SLO/smoke regression          (ARCHITECTURE.md §12)
SCHEDULED (non-blocking):  full E2E, load/soak, mutation tests, dependency-CVE re-scan
```

- **Fast-fail:** cheapest checks first; stop at first failure (TESTING.md §11).
- **The gate blocks the merge** — a check that doesn't block is decoration.
- **Keep the gating suite fast** (parallel/shard/affected-only) or people route around it.
- **If a gate is missing, that's tracked debt** — flag it and add it (every deep doc says this).

---

## 9. Documentation & Knowledge Standard

- **README is the entry point** (§3): run/test/contribute + links to this doc set.
- **ADRs for significant/irreversible decisions** in `/docs/decisions` (ARCHITECTURE.md §14) —
  the durable "why."
- **Update docs in the same PR** that changes behavior, API, setup, or domain language
  (new domain term → GLOSSARY.md in the same change — §2).
- **Comments explain *why*, not *what*** (CONVENTIONS.md §5). Code is the source of truth for
  *what*; keep docs from going stale by updating-with-the-code.
- **This doc set is living:** when a standard genuinely changes, update the owning doc (and ADR
  the decision) — don't let docs and reality diverge (the cardinal sin of every standard).

---

## 10. Instructions for AI Agents (binding)

If you do engineering work here, this binds you. It governs *process*; the deep docs govern
*topic rules* (and win on their own topic — §intro).

1. **Meet the Definition of Done (§2) before calling anything complete** — and only claim a gate
   passed if you *actually ran it* (AGENTS.md verification; honesty rules across all docs).
2. **Keep changes small, focused, and reviewable (§4).** One logical change. Don't mix
   feature+refactor+reformat. Don't sprawl beyond the task's scope.
3. **Follow the repo's structure and put things in the canonical place (§3, ARCHITECTURE.md §4).**
   Don't invent new top-level folders or scatter duplicates; match the existing tree.
4. **Follow the git/branch/commit workflow (§5)** and the **commit/VCS behavior rules in
   AGENTS.md** (don't commit/push/stash unless asked).
5. **Produce a PR-ready change (§6):** when you finish, give the WHAT/WHY/HOW/RISK/TESTING summary
   so a human can review it — including exactly what you ran and verified, and what you did not.
6. **Defer to the deep docs for topic rules:** code style → CONVENTIONS.md; folders/boundaries →
   ARCHITECTURE.md; security → SECURITY.md; tests → TESTING.md; UI → DESIGN.md; naming →
   GLOSSARY.md. Don't reinvent or contradict them; if they conflict with this file, the deep doc
   wins on its topic.
7. **Separate refactors from behavior changes (§4)** — and when refactoring, prove behavior is
   unchanged with tests (TESTING.md §4).
8. **Update docs/ADR/GLOSSARY in the same change** when behavior, API, decisions, or domain
   vocabulary change (§2, §9).
9. **Honesty (cardinal, shared across all docs):** never report DoD items, gates, reviews, or
   tests as satisfied unless they actually are. State what's done, what's assumed, what still
   needs human review. Right-sized and honest beats impressive and faked.
10. **If a process gate is missing (§8)** — no PR template, no CODEOWNERS, no pre-commit, no CI
    gate — flag it and offer to add it; don't silently rely on prose.

---

## 11. Quality Checklist (the process checklist — run before opening/merging a PR)

```
CHANGE
[ ] One logical change; small, focused diff; refactor separated from behavior change.
[ ] No unrelated reformatting/churn; files in their canonical location.

DEFINITION OF DONE (§2)
[ ] Build green; format+lint+types pass; tests added + suite green; patterns/naming match.
[ ] Architecture boundaries respected; security rules met; UI states handled (if UI).
[ ] No dead code/debug prints/secrets; docs/ADR/GLOSSARY updated if needed.
[ ] Every claimed check actually run (no faked greens).
[ ] PRODUCT.md updated if this change altered product scope/target/core-job/non-goal/constraint.

PR (§6)
[ ] Description answers WHAT/WHY/HOW/RISK/TESTING/ROLLBACK; screenshots for UI.
[ ] Linked to issue/ADR; CI green BEFORE review requested; self-reviewed the diff.
[ ] Risk assessed; high-risk → SAFE MODE handling + CODEOWNERS review.

REVIEW (§7)
[ ] Reviewed for correctness→design→security→tests→readability (in that order).
[ ] Blocking vs nit distinguished; tooling (not humans) handled style nits.
[ ] Required approvals + CODEOWNERS on critical paths; branch protection satisfied.

GUT CHECK
[ ] Would a reviewer understand this PR without being in my head?
[ ] Is `main` still shippable after this merges? Can I cleanly roll it back?
[ ] Did I keep it small, or should this have been multiple PRs?
```

---

## References

- *Software Engineering at Google* — Winters/Manshreck/Wright (code review, small CLs,
  trunk-based dev, ownership, the engineering process at scale).
- *Accelerate* — Forsgren/Humble/Kim (small batches, trunk-based dev, fast review, and CD
  *correlate with* high performance — the empirical backing for §1, §4, §5).
- *The Pragmatic Programmer* — Hunt & Thomas; *A Philosophy of Software Design* — Ousterhout
  (change size, complexity, leaving code better).
- **Conventional Commits**, **trunk-based development** (trunkbaseddevelopment.com), GitHub/GitLab
  flow, CODEOWNERS + branch protection (the mechanics of §5–§8).
- **The deep standards this file ties together:** AGENTS.md · ARCHITECTURE.md · SECURITY.md ·
  DESIGN.md · CONVENTIONS.md · TESTING.md · GLOSSARY.md (each owns its topic; this owns the
  process that connects them).

---

*This file is the operating manual — how work flows from branch to review to ship — not a
restatement of the deep standards. It owns the process (Definition of Done, PR/review rules, git
workflow, folder defaults, the unified gate); the deep docs own the craft. Together they make
work from any author, human or AI, structured the same way and held to the same bar before it
lands. The process is the multiplier; the gate keeps it honest.*
```

---