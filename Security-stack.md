# SECURITY-STACK.md — SecureDeviceControl Security Tooling

> Concrete companion to SECURITY.md.

---

## 1. Enforcement tooling

- **Secret scanning (pre-commit + CI):** `dotnet list package --vulnerable` in CI (NuGet audit); recommend gitleaks (MISSING — tracked gap)
- **SAST (CI):** none yet (MISSING — tracked gap)
- **Dependency/SCA scan (CI):** `dotnet list package --vulnerable --include-transitive` in GitHub Actions
- **Build fails on:** vulnerable package findings (warn in MVP; tighten post-MVP)
- **CODEOWNERS on:** auth, crypto, service host (to add)

## 2. Auth & crypto choices

- **Auth approach:** 6-digit PIN validated in service only; short-lived session token (15 min default)
- **Password hashing:** Argon2id (Konscious.Security.Cryptography.Argon2)
- **Validation library:** manual validation at IPC boundary + domain guards
- **Secret store:** DPAPI-protected SQLite row for PIN hash; no secrets in repo or desktop process

## 3. Compliance scope

- **none** (local tool); device identifiers in ActivityLog only; no PIN/session tokens in logs

## 4. Security tests in place

- [x] unauth mutating IPC → Unauthorized
- [x] invalid PIN → InvalidPin with rate limiting
- [x] session expiry → SessionExpired
- [x] malformed IPC frames rejected
- [ ] pipe ACL bypass (manual pen-test)
- [ ] PIN brute-force (integration test covers rate limit)
