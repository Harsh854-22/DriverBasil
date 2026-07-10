## 6. `ENGINEERING-STACK.md`

```markdown
# ENGINEERING-STACK.md — Project-Specific Process Config

> Concrete companion to ENGINEERING_STANDARDS.md. Holds branch scheme, CI, PR template, repo tree. FILLED PER PROJECT.

---
## 0. FILL PROTOCOL (binding)
> **AI agents: detect or ASK — never assume the workflow.**
> 1. DETECT: `.github/` workflows + templates, CODEOWNERS, branch naming in git history, merge style.
> 2. STATE what exists / what's MISSING (missing gate = tracked debt — ENG §8).
> 3. If new/empty or unclear → ASK: "CI provider? Merge style (squash default)? Branch scheme? PR template wanted?"
> 4. Fill below; offer to ADD missing pieces (PR template, CODEOWNERS, pre-commit, CI gate).

## 1. Git workflow (ENG §5)  `[FILL]`
- **Branching:** `[FILL: trunk-based | short-lived feature branches]`
- **Branch naming:** `[FILL: type/short-desc]`  · **Merge style:** `[FILL: squash (default) | rebase]`
- **Commit convention:** `[FILL: Conventional Commits y/n]`

## 2. CI/CD & gate (ENG §8)  `[FILL — flag missing]`
- **CI provider:** `[FILL: GitHub Actions | GitLab CI | …]`
- **Gate order:** format → lint → typecheck → unit → integration → security → E2E `[confirm/adjust]`
- **Branch protection:** `[FILL: required green + N reviews + CODEOWNERS?]`
- **Deploy strategy:** `[FILL: canary | blue-green | rolling]`  · **Auto-rollback:** `[FILL y/n]`
- **PR-size nudge bot:** `[FILL: danger.js / diff-size check | none]`

## 3. Repo tree (ENG §3)  `[FILL the ACTUAL tree]`
```
[FILL: the real top-level structure of THIS repo]
```

## 4. Doc set location  `[FILL]`
- Standards live in: `[FILL: repo root | /docs]`  · PR template: `[FILL path or "to add"]`
```

---
