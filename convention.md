# CONVENTIONS.md — Code Conventions & Consistency Standard

> **Status:** Mandatory for all engineers and AI agents in this repo.
> **Goal:** Code from any author (human or any AI model) reads as if written by one disciplined
> engineer. Consistency is the product. Differences in *style* must vanish so differences in
> *logic* are visible in review.
> **Priority:** Consistency > Readability > Simplicity > Cleverness (cleverness is last on purpose).
>
> **THE SINGLE MOST IMPORTANT THING IN THIS FILE:**
> ~70% of "this looks like a different person wrote it" is **formatting**, and a formatter
> eliminates 100% of it automatically. **The auto-formatter is the source of truth for style —
> not this document, not your preference, not any model's default.** Run it on every file before
> done. This doc governs the ~30% a formatter can't enforce: naming, structure, error handling,
> comments, and patterns. (See §1 and §13.)

---

## 0. How to Use This File

- **This file is language-agnostic (the universal ~70%).** Per-language specifics (exact naming
  case, formatter, linter, idioms, file layout) live in **`CONVENTIONS-STACK.md`** — fill that
  in once per project. If it's missing, that's a gap: flag it (§14).
- **Humans:** internalize §1–§2; use §15 as a review lens.
- **AI agents:** §14 is binding. The overriding rule is §1.1 — *match the existing code over
  every rule in this file*. A consistent repo that disagrees with this doc beats an "ideal" repo
  that's internally inconsistent.

---

## 1. The Prime Directives (these override everything else)

1. **Match the existing codebase first.** The conventions *already in the repo* outrank this
   document. If the repo uses a pattern, follow it even if you'd personally do otherwise. Read a
   neighboring file before writing; mirror its structure, naming, and idioms. Consistency with
   what exists > theoretical correctness.
2. **The auto-formatter is the source of truth for formatting.** Never hand-format. Never argue
   about spaces/quotes/semicolons/line-length — the formatter decides, run it, move on. (§13)
3. **The linter is the source of truth for lintable rules.** If the linter is configured, its
   rules win; don't disable a rule inline without a stated reason in a comment.
4. **Code is read far more than written.** Optimize every choice for the reader, not the writer.
   Clarity beats brevity; brevity beats cleverness.
5. **Least surprise.** A reader should be able to predict what a function does from its name and
   signature, and where a thing lives from its name. No surprises, no magic.

---

## 2. Naming (the highest-value convention prose can enforce)

Naming is where formatters can't help and where inconsistency screams loudest.

- **Reveal intent.** A name should answer *what it is / what it does / what it returns* without
  needing a comment. `getActiveUsersSince(date)` not `getData(d)`.
- **Searchable & pronounceable.** No single letters (except trivial loop indices `i`/`j` and
  conventional `_` for unused), no cryptic abbreviations. `userCount` not `usrCnt`.
- **Length scales with scope.** Tiny scope → short name; module/exported → descriptive name.
- **Consistent vocabulary (no synonyms).** Pick one term per concept and use it everywhere —
  `get` vs `fetch` vs `retrieve`, `delete` vs `remove`, `user` vs `customer`. Define the canonical
  terms in `GLOSSARY.md`. Mixing synonyms is a defect.
- **Functions = verbs / verb phrases.** `createOrder`, `isValid`, `hasAccess`, `calculateTotal`.
- **Booleans read as predicates.** Prefix `is`/`has`/`should`/`can`: `isLoading`, `hasPermission`.
- **Collections are plural.** `users`, `orderIds`. Singular for one: `user`.
- **No type/encoding in the name** when the type system already says it (`userList: User[]` →
  `users: User[]`). No Hungarian notation.
- **No noise words.** `userData`, `userInfo`, `userObject` → just `user`. `theUser`, `myUser` → no.
- **Case style is per-language — see `CONVENTIONS-STACK.md`.** (e.g. `camelCase`/`PascalCase` for
  TS/Java; `snake_case` for Python/Rust; `PascalCase` exports for Go.) **Never** impose one
  language's casing on another — that's the #1 "AI wrote this" tell across languages.
- **Constants:** the language's constant convention (commonly `UPPER_SNAKE_CASE`); no magic
  numbers/strings inline — name them.

---

## 3. Functions & Methods

- **One job.** A function does one thing at one level of abstraction. If you need "and" to
  describe it, split it.
- **Small.** Prefer short functions; extract when a block needs a comment to explain *what* it
  does (the extracted name becomes the comment). Hard caps belong in the linter, not your head.
- **Few parameters.** Aim ≤ 3. Beyond that, pass an options object/struct. Avoid boolean
  parameters that flip behavior — split into two functions or use an enum.
- **No hidden side effects.** A function named `getX` must not mutate state or do I/O surprises.
  Command/query separation: a function either *does* something or *answers* something, ideally
  not both.
- **Return early.** Guard clauses over deep nesting. Flatten the happy path.
- **Pure where possible.** Push side effects (I/O, mutation, time, randomness) to the edges; keep
  the core logic pure and testable (ties to ARCHITECTURE.md §4 — domain free of IO).
- **Explicit over implicit.** No relying on hidden globals, ambient state, or magic context.

---

## 4. Files, Modules & Structure

- **One primary concept per file.** A file should have a clear single responsibility; its name
  states it. (Components: one component per file is the norm.)
- **Consistent intra-file order.** Use one order everywhere (define the exact order in
  `CONVENTIONS-STACK.md`); a common one:
  ```
  1. Imports (grouped: stdlib/external → internal → types → styles)
  2. Types / interfaces / constants
  3. The main export(s)
  4. Helpers (below what uses them — read top-down, like a newspaper)
  ```
- **Group by feature/domain, not by technical type**, once the project grows (matches
  ARCHITECTURE.md §4): prefer `features/billing/{ui,logic,data}` over global `controllers/`,
  `services/`, `models/`. Small projects may stay flat — be consistent.
- **Imports:** absolute/aliased imports over deep relative chains (`@/features/x` not
  `../../../x`). Group and let the formatter/linter order them.
- **File length is a smell, not a law.** A very long file usually means it's doing too much —
  split by responsibility, not by arbitrary line count.

---

## 5. Comments & Documentation

- **Comment WHY, not WHAT.** The code says what; comments explain intent, trade-offs, the
  non-obvious, the "why this weird thing exists." `// retry: vendor API returns 200 on failure`.
- **The best comment is a better name.** If code needs a comment to be understood, first try to
  make it self-explanatory (rename, extract).
- **No dead code, no commented-out code** in commits. Delete it — version control remembers.
- **No redundant comments.** `i++ // increment i` is noise. `// users` over `const users` is noise.
- **Doc-comments on public/exported API** (the language's doc convention — JSDoc/docstrings/
  rustdoc/godoc): what it does, params, returns, throws, and gotchas. Internal helpers usually
  don't need them.
- **Mark debt explicitly:** `TODO(owner): …` / `FIXME(owner): …` with enough context to action
  it. Don't leave anonymous TODOs to rot; track meaningful ones.
- **Keep comments true.** A stale comment is worse than none — update or delete it with the code.

---

## 6. Error & Exception Handling (define the ONE pattern in CONVENTIONS-STACK.md)

Inconsistent error handling is one of the loudest "different author" signals. Pick **one**
project-wide pattern and use it everywhere.

- **Choose the project's error model and stick to it:** exceptions, or a `Result`/`Either`
  return type, or Go-style `(value, err)`. Document the chosen one in `CONVENTIONS-STACK.md`;
  don't mix paradigms within the codebase.
- **Never swallow errors silently.** No empty `catch {}`. Handle, or propagate with context, or
  deliberately ignore *with a comment explaining why*.
- **Add context as errors propagate** ("failed to load user 123: <cause>"), preserving the
  original cause/stack (wrap, don't replace).
- **Catch specific errors, not blanket catch-alls** (except at a top-level boundary that logs +
  returns a clean response).
- **Fail fast on programmer errors** (bad invariants, impossible states); handle gracefully for
  expected operational errors (network, validation, not-found).
- **Errors to users are generic + a correlation id; full detail goes to logs** (mirrors
  SECURITY.md §10 and ARCHITECTURE.md). Never leak stack traces/internals to clients.
- **Validate at boundaries** (input, external responses); inside the validated core, trust your types.
- **Clean up resources** reliably (defer/finally/context-manager/`using`) — no leaked
  connections/handles on error paths.

---

## 7. Types & Data

- **Maximize type safety.** Strict mode on. No untyped escape hatches (`any`, untyped `dict`,
  `interface{}`/`any` in Go, `unwrap()` everywhere) without a stated reason — they defeat the
  consistency the type system gives you across authors.
- **Type at boundaries; infer inside.** Annotate public signatures and module edges explicitly;
  let inference handle obvious locals (per language norms in `CONVENTIONS-STACK.md`).
- **Make illegal states unrepresentable.** Prefer enums/unions/sum-types over stringly-typed
  flags; prefer a precise type over a loose bag of optionals.
- **Immutability by default.** Prefer `const`/`final`/`readonly`/`val`; mutate deliberately, not
  habitually. Don't mutate function arguments.
- **Null/undefined/None discipline:** be explicit about optionality in the type; handle the
  empty case; avoid passing/returning null where the type can express "absent" properly.
- **Parse, don't validate-and-forget:** turn unstructured input into a trusted typed value once,
  at the edge, then pass the typed value inward.

---

## 8. Control Flow & Logic

- **Guard clauses + early return** over nested conditionals. Keep nesting shallow (≤ ~3 levels).
- **No magic values.** Name numbers and strings that carry meaning.
- **Exhaustive handling.** Handle every case of a union/enum (lean on the compiler to enforce it).
- **Prefer declarative over imperative** where it's clearer (map/filter/reduce over manual loops)
  — but not when it obscures intent. Clarity wins over both.
- **One way to do a common thing.** If the repo has a helper for X, use it — don't reinvent.
- **Avoid clever one-liners** that need decoding. Cleverness is the lowest priority (§1).

---

## 9. Dependencies & Imports

- **Add dependencies sparingly and deliberately.** Each one is a security, maintenance, and
  consistency cost (ties to SECURITY.md §11). Prefer the standard library; justify new deps; ASK
  before adding one (per AGENTS.md).
- **Use the repo's existing library for a job** before introducing a competing one (don't add
  `axios` if the repo standardizes on `fetch`, don't add a second date lib, etc.).
- **No deep imports into a package's internals;** import from its public surface.
- **Pin/lock versions** (lockfile committed) — consistency across machines and authors.

---

## 10. Tests (consistency applies to tests too)

(Full testing policy lives in `TESTING.md` if present; these are the *convention* aspects.)

- **Consistent naming & structure:** describe the unit + the behavior
  (`describe("createOrder") > it("rejects an empty cart")`). One clear pattern repo-wide.
- **Arrange–Act–Assert** (Given–When–Then) structure; one logical assertion concept per test.
- **Test behavior, not implementation.** Names read as specifications of intent.
- **Deterministic:** no real network/clock/randomness — inject or mock them. No flaky tests.
- **No skipped/disabled tests committed** without a tracked reason.
- **Test data is realistic**, named meaningfully (not `foo`/`bar` for domain objects).

---

## 11. Formatting & Whitespace — *delegated entirely to the formatter*

- **Do not hand-format. Run the formatter.** Line length, indentation, quotes, semicolons,
  trailing commas, import spacing, bracket placement — **all decided by the configured formatter**
  (`prettier`/`black`/`gofmt`/`rustfmt`/`ktlint`/etc., named in `CONVENTIONS-STACK.md`).
- **The formatter config is committed and is the only style authority.** No personal overrides,
  no editor-specific reformatting, no manual "tidying" that fights the formatter.
- **This is the mechanism that makes any model's output byte-identical.** It is non-negotiable.

---

## 12. Git & Commit Conventions

(VCS *behavior* — when to commit — is governed by AGENTS.md. This is the *format* convention.)

- **Commit messages:** short imperative subject ("Add retry to payment client"), ≤ ~72 chars,
  body explains *why* if non-obvious. Match the repo's existing style; adopt **Conventional
  Commits** (`feat:`, `fix:`, `refactor:`…) if the repo already does.
- **Atomic commits:** one logical change per commit; don't mix refactor + feature + format churn.
- **No noise in diffs:** don't reformat untouched code in a feature PR (it hides the real change).
- **Branch naming:** follow the repo's existing scheme (record it in `CONVENTIONS-STACK.md`).

---

## 13. The Enforcement Stack (doctrine doesn't enforce — tooling does)

Same principle as SECURITY.md (§13) and ARCHITECTURE.md (§18): **this prose can't enforce
consistency — automation can, and that's what makes cross-author/cross-model code identical.**
Expected in the repo; if absent, it's a tracked gap (flag and offer to add — §14):

- **Formatter** (`prettier`/`black`/`gofmt`/`rustfmt`/…): the style source of truth (§11).
- **Linter** (`eslint`/`ruff`/`golangci-lint`/`clippy`/…): catches anti-patterns, enforces
  naming/complexity/import rules, forbids `any`/dead code/etc. **Build fails on lint errors.**
- **Type checker in strict mode** (`tsc --strict`/`mypy --strict`/compiler): the most powerful
  consistency tool — it forces every author/model into the same type discipline.
- **Pre-commit hooks** (`husky`/`lint-staged`/`pre-commit`): format + lint + typecheck before a
  commit can land — so inconsistent code *physically cannot* enter the repo.
- **CI gate:** format-check + lint + typecheck + tests on every PR; **block merge on failure.**
- **EditorConfig** (`.editorconfig`): baseline consistency across editors/IDEs.

> Exact tools, configs, and thresholds live in `CONVENTIONS-STACK.md`. Keep selection there so
> this doctrine stays stack-agnostic and the tooling stays specific — same split as the other docs.

---

## 14. Instructions for AI Agents (binding)

If you write or modify code here, this binds you.

1. **Match the existing codebase before this document (§1.1).** Read a neighboring file first;
   mirror its naming, structure, error-handling, and idioms. Repo consistency outranks every
   rule below. When the repo and this doc conflict, follow the repo and note it.
2. **Read `CONVENTIONS-STACK.md` (and `GLOSSARY.md`) if present** for the language-specific
   casing, formatter, linter, error model, file order, and canonical vocabulary. If absent and
   it matters, ASK or state your assumption explicitly before proceeding.
3. **Run the formatter on every file you touch (§11).** Never hand-format, never fight the
   formatter, never reformat untouched code in a feature change (§12).
4. **Use the repo's chosen error model, type discipline, and libraries — don't introduce a
   competing paradigm or dependency** (§6, §7, §9). No new dependency without asking (AGENTS.md).
5. **Naming: reveal intent, one canonical term per concept (GLOSSARY.md), correct per-language
   casing.** No synonyms, no noise words, no cryptic abbreviations (§2).
6. **No dead code, no commented-out code, no anonymous orphan TODOs, no debug prints** left in
   committed code (§5). Comment *why*, not *what*.
7. **Don't claim "consistent," "clean," or "formatted" unless you actually ran the
   formatter/linter/typechecker** (mirrors AGENTS.md verification + SECURITY/ARCHITECTURE honesty
   rules). State which you ran. An unrun check is not a passed check.
8. **Counteract your defaults:** different models default to different naming, comment density,
   error styles, and import ordering. Suppress your house style; adopt *this repo's* style. The
   goal is that no reviewer can tell which model wrote which file.
9. **If the enforcement stack (§13) is missing,** flag it as a consistency gap and offer to add
   the formatter/linter/typecheck/pre-commit config — don't silently rely on prose.

---

## 15. Quality Checklist (run before calling code "done")

```
CONSISTENCY
[ ] Matches neighboring files (naming, structure, idioms, error handling).
[ ] Formatter run; linter clean; typecheck passes (strict). (Actually ran — not assumed.)

NAMING
[ ] Names reveal intent; correct per-language casing; canonical vocabulary (no synonyms).
[ ] Functions are verbs; booleans are predicates; no noise words / cryptic abbreviations.
[ ] No magic numbers/strings — named.

FUNCTIONS & STRUCTURE
[ ] One job per function; small; ≤3 params (or options object); early returns; shallow nesting.
[ ] Side effects pushed to edges; core logic clear; one concept per file; sane file order.

ERRORS & TYPES
[ ] Project's single error model used; no swallowed errors; context added; resources cleaned up.
[ ] Strict types; no unjustified `any`/escape hatches; illegal states hard to represent.
[ ] Input validated at boundaries; generic errors to users + correlation id (SECURITY.md).

COMMENTS & HYGIENE
[ ] Comments explain WHY; public API documented; no dead/commented-out code; no debug prints.
[ ] TODOs are owned and actionable.

DEPENDENCIES & TESTS
[ ] No needless new deps; uses the repo's existing libraries; lockfile updated if changed.
[ ] Tests named as behavior; AAA structure; deterministic; realistic data; none skipped.

GIT
[ ] Atomic commit; imperative message in repo's style; no unrelated reformatting in the diff.

GUT CHECK (the human/reviewer backstop)
[ ] Could a reviewer tell a different person/model wrote this vs. the rest of the repo? If yes — fix it.
[ ] Is it optimized for the next reader, not for cleverness?
```

---

## References (the actual sources)

- **Books:** *Clean Code* — Martin (naming, functions, comments — apply with judgment, not
  dogma); *The Pragmatic Programmer* — Hunt & Thomas; *Code Complete* — McConnell;
  *A Philosophy of Software Design* — Ousterhout (deep modules, complexity, comments — an
  excellent counterweight to Clean Code dogma); *Refactoring* — Fowler.
- **Style guides to model `CONVENTIONS-STACK.md` on:** Google Style Guides (multi-language),
  Airbnb JavaScript/TypeScript, PEP 8 + the Black/Ruff defaults (Python), Effective Go +
  `gofmt` (Go), the Rust API Guidelines + `rustfmt`/`clippy` (Rust). **Adopt a published guide
  wholesale rather than inventing one** — proven, complete, and tooling already exists for it.
- **Tooling = the real enforcement:** Prettier/Black/gofmt/rustfmt (format), ESLint/Ruff/
  golangci-lint/Clippy (lint), tsc/mypy strict (types), Husky/pre-commit (gates),
  Conventional Commits (messages), EditorConfig (editor baseline).
- **Convention ≠ dogma:** the goal is *one consistent voice*, not the "objectively best" style.
  The best convention is the one your whole team/repo actually follows and your tooling enforces.

---

*Consistency is the feature. This document encodes the ~30% of conventions a formatter can't —
the formatter, linter, and type checker (§13) enforce the rest automatically, which is what makes
code from any author or any AI model read as one hand. The document sets the conventions; the
tooling makes them inevitable; the checklist keeps it honest.*
```

---
