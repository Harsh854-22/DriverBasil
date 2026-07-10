## 4. `TESTING-STACK.md`

```markdown
# TESTING-STACK.md — Project-Specific Testing Tooling

> Concrete companion to TESTING.md. Records frameworks, browser tool, thresholds. FILLED PER PROJECT.

---
## 0. FILL PROTOCOL (binding)
> **AI agents: detect or ASK — never assume the test stack.**
> 1. DETECT: test runner in deps/config, existing test file patterns, CI test steps, browser tooling.
> 2. STATE detected stack; if new/empty or ambiguous → ASK: "Which test runner? Mocking approach?
>    Browser E2E tool (Playwright/Cypress) — and does the agent have browser access? Coverage floor? Load tool?"
> 3. Fill below; match existing tests' style (CONVENTIONS.md §1.1) before inventing.

## 1. Frameworks  `[FILL]`
- **Unit/integration runner:** `[FILL: Vitest | Jest | pytest | go test | cargo test | JUnit | …]`
- **Assertion/mocking:** `[FILL]`  · **Real ephemeral DB for integration:** `[FILL: Testcontainers | in-mem | …]`
- **Test file convention:** `[FILL: *.test.ts beside source | tests/ dir | _test.go | …]`

## 2. Browser/E2E (if UI)  `[FILL — incl. agent access]`
- **Tool:** `[FILL: Playwright (preferred) | Cypress | none]`
- **Agent browser access:** `[FILL: yes (Playwright MCP/computer-use) | no — write tests for CI]`
- **Locator strategy:** `[FILL: role/label/text or data-testid — never brittle CSS]`
- **A11y scan:** `[FILL: axe-core | none]`  · **Visual regression:** `[FILL or none]`

## 3. Coverage & quality  `[FILL — floors, NOT 100% targets]`
- **Coverage floor (core logic):** `[FILL: e.g. 80% on domain — TESTING.md §8]`  (anti-goal: 100%)
- **Mutation testing (critical modules):** `[FILL: Stryker/PIT/mutmut | none]`
- **Property testing:** `[FILL: fast-check/Hypothesis | none]`

## 4. Scale/resilience tooling (TESTING.md §9)  `[FILL — flag if none; must run vs real infra]`
- **Load:** `[FILL]`  · **Chaos:** `[FILL or "n/a at this tier"]`
```

---



