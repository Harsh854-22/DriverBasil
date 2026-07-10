# CONFIGS-TO-GENERATE.md — The Enforcement Wall (agent: detect stack, then generate)

> The real consistency mechanism. Agent: detect the stack (or ASK), then generate the
> stack-appropriate config for EACH row. Flag any that can't be added.

| Need | TS/JS | Python | Go | Rust | JVM | Generate as |
|------|-------|--------|----|----|-----|-------------|
| Formatter | Prettier | Black/Ruff-format | gofmt | rustfmt | spotless/ktlint | [stack config] |
| Linter | ESLint | Ruff | golangci-lint | Clippy | Checkstyle/detekt | [stack config] |
| Types (strict) | tsconfig `strict:true` | mypy strict | (compiler) | (compiler) | (compiler) | [stack config] |
| Pre-commit | husky + lint-staged | pre-commit | pre-commit | pre-commit | pre-commit | format+lint+typecheck hook |
| CI gate | GitHub Actions | " | " | " | " | the §8 pipeline order |
| Editor baseline | .editorconfig | " | " | " | " | .editorconfig |
| Boundary lint (ARCH §18) | dependency-cruiser | import-linter | go-arch-lint | — | ArchUnit | [stack config] |

**Agent protocol:** detect stack → STATE it → if ambiguous ASK → generate each config to match
the `*-STACK.md` choices → wire the CI gate in ENG §8 order → confirm all gates block merges.
The formatter + strict types are the single biggest levers for cross-model identical output
(CONVENTIONS.md §11) — prioritize those.
```

---
