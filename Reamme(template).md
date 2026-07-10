## 7. `README.md` (template)

```markdown
# [Project Name]  `[FILL]`

> [One-sentence description of what this is and who it's for.]  `[FILL]`

## What this is
`[FILL: 1 short paragraph]`

## Tech Stack
`[FILL — or: "see ARCHITECTURE-STACK.md". Agent: detect or ASK before filling.]`

## Getting Started
```bash
# Install
[FILL: install command]
# Configure
cp .env.example .env   # then fill in values (see .env.example)
# Run (dev)
[FILL: dev command]
# Test
[FILL: test command]
# Build
[FILL: build command]
```

## Project Structure
`[FILL: brief — or point to ENGINEERING-STACK.md §3]`

## Contributing & Standards
This project is governed by a standards doc set. **Read before contributing (human or AI):**

| Doc | Purpose |
|-----|---------|
| `SYSTEM_PROMPT.md` | Operating contract for AI agents (read first if you're an agent) |
| `ENGINEERING_STANDARDS.md` | Process, PR rules, Definition of Done |
| `ARCHITECTURE.md` (+`-STACK`) | System design |
| `CONVENTIONS.md` (+`-STACK`) | Code style |
| `SECURITY.md` (+`-STACK`) | Security |
| `DESIGN.md` (+`DESIGN-TOKENS`) | UI/UX (if applicable) |
| `TESTING.md` (+`-STACK`) | Testing |
| `GLOSSARY.md` | Domain vocabulary |

- Definition of Done: see `ENGINEERING_STANDARDS.md §2`.
- All checks must pass before merge (see the gate: `ENGINEERING_STANDARDS.md §8`).

## License
`[FILL]`
```

---

## 8. `.env.example` (template — SECURITY.md §3)

```bash
# .env.example — committed template with PLACEHOLDER values only.
# Real .env is gitignored (SECURITY.md §3). Never commit real secrets.
# AI agents: detect required vars from the codebase or ASK; never invent real credentials.

# --- App ---
NODE_ENV=development          # [FILL per stack: ENV/APP_ENV/etc.]
PORT=3000                     # [FILL]

# --- Database ---           # [FILL or remove if no DB]
DATABASE_URL=YOUR_DATABASE_URL_HERE

# --- Auth / Secrets ---     # [FILL only what you use]
JWT_SECRET=YOUR_JWT_SECRET_HERE
SESSION_SECRET=YOUR_SESSION_SECRET_HERE

# --- Third-party (examples — keep only real ones) ---
# STRIPE_SECRET_KEY=YOUR_STRIPE_KEY_HERE
# SMTP_URL=YOUR_SMTP_URL_HERE

# --- Add real keys above as placeholders; NEVER real values in this file. ---
```
