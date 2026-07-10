# ARCHITECTURE-STACK.md — SecureDeviceControl Architecture

> Concrete companion to ARCHITECTURE.md.

---

## 1. Requirements snapshot (ESTIMATE — pre-launch MVP)

- **Criticality:** irreversible (security enforcement) · **Users/RPS:** 1 admin, <1 RPS
- **Latency SLO:** p95 < 500ms IPC · **Availability:** best-effort local service · **Consistency:** strong (single SQLite writer in service)
- **Team size:** 1–3 · **Compliance:** local device identifiers only; no cloud PII

## 2. Chosen architecture

- **Tier:** Tier 0 — **why:** single-machine, offline, no horizontal scale requirement
- **Shape:** Two-process modular split — WPF Desktop (presentation) + Windows Service (enforcement)
- **Language/framework:** .NET 8 (C#), WPF desktop, `BackgroundService` Windows Service
- **Datastore:** SQLite under `%ProgramData%\SecureDeviceControl\` (service-owned); DPAPI for secret protection
- **IPC:** Named Pipes (`\\.\pipe\SecureDeviceControl\v1`) with JSON framed messages
- **Cache/queue/CDN:** none
- **Deploy target:** Local Windows install (dev: PowerShell script)

## 3. Verification tooling

- **Dependency/boundary linter:** project reference rules (Domain has no Infrastructure/Desktop refs)
- **Load tool:** n/a at Tier 0
- **Contract test:** Shared IPC contract + integration tests over real Named Pipe
- **N+1 / query gate:** n/a (SQLite, paginated queries)
- **Migration tool:** manual SQL init in Infrastructure

## 4. ADR index

- [ADR-0001](docs/decisions/ADR-0001-two-process-named-pipe-ipc.md) — Two-process architecture + Named Pipe IPC
- [ADR-0002](docs/decisions/ADR-0002-pin-hashing-dpapi.md) — PIN hashing (Argon2id) + DPAPI-protected config
- [ADR-0003](docs/decisions/ADR-0003-device-control-setupapi.md) — Device control via Configuration Manager / SetupAPI
