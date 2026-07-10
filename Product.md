# PRODUCT.md — SecureDeviceControl

> **Status:** Single source of truth for product intent. Binding on all engineers and AI agents.

---

## 1. The One-Liner & The Problem

```
WHAT IT IS (one sentence):  A native Windows security tool that helps local administrators control USB/removable storage access via PIN-protected policy enforcement.

THE PROBLEM:                Uncontrolled USB and removable storage devices are a common data-exfiltration and malware vector on Windows workstations. Built-in OS controls are fragmented, easy to bypass, and lack a focused admin workflow with audit visibility.

WHY IT MATTERS:             Organizations and security-conscious individuals need deterministic, auditable device control without cloud dependency or complex enterprise tooling.

WHAT SUCCESS LOOKS LIKE:    An administrator can block unknown removable storage by default, trust specific devices, temporarily relax policy with an unlock timer, and review every enforcement action — all from a local desktop app backed by a privileged Windows Service.
```

---

## 2. Who It's For (users & their goals)

| User / persona | Their goal (the job-to-be-done) | Their context / constraint |
|----------------|--------------------------------|----------------------------|
| **Local Windows Administrator** | Prevent unauthorized USB/removable storage use; allow exceptions when needed | Single-machine or small fleet; offline-capable; needs clear audit trail |
| **Security-conscious power user** | Lock down their own PC without enterprise MDM | Runs locally; expects PIN gate before policy changes |

- **Primary user (optimize for this one):** Local Windows Administrator on a single machine
- **Explicitly NOT for:** Enterprise fleet management, remote administration, non-Windows platforms, or users without local admin rights to install the service

---

## 3. Core Jobs / Features (what it must do — ranked)

| # | Capability | Purpose (the user need it serves) | Priority |
|---|-----------|-----------------------------------|----------|
| 1 | **PIN authentication** | Gate all privileged operations behind a 6-digit PIN validated only in the service | Must |
| 2 | **Device policy enforcement** | Block new removable storage by default; enforce on connect and policy change | Must |
| 3 | **Trusted device management** | Allow known-safe devices to bypass the default block | Must |
| 4 | **Unlock timer** | Time-bounded relaxation of policy for maintenance windows | Must |
| 5 | **Activity log** | Append-only audit of auth, policy, and device events | Must |
| 6 | **Dashboard & device list** | Real-time visibility into service status, connected devices, and policy state | Must |
| 7 | **Security settings** | Change PIN, configure session timeout, default policy mode | Should |
| 8 | **Export activity logs** | CSV export for offline review | Could |

- **The ONE job, if we could only do one:** Block unauthorized removable storage unless explicitly trusted or covered by an active unlock timer.

---

## 4. Explicit Non-Goals (what we are deliberately NOT building)

- ❌ Web dashboard or cloud console
- ❌ Mobile apps (iOS/Android)
- ❌ Remote administration or multi-machine sync
- ❌ Organization accounts, SSO, or directory integration
- ❌ Internet-dependent features or telemetry
- ❌ Full device-class coverage beyond USB/removable storage in MVP (network adapters, printers deferred)
- ❌ Production installer (WiX/MSI) — dev install script only for MVP

---

## 5. Constraints & Context

```
STAGE:            MVP
CRITICALITY:      irreversible (security enforcement — fail-closed required)
SCALE EXPECTATION: Single machine, low traffic; high-stakes correctness
PLATFORM(S):      Windows desktop (WPF) + Windows Service
COMPLIANCE:       Local PII minimal (device identifiers only; no PIN in logs)
KEY CONSTRAINTS:  Offline-only; two-process architecture; all enforcement in service
BRAND / TONE:     Minimal/technical, information-dense security admin tool; light + dark themes
```

---

## 6. Key Terms (pointer)

- **Administrator** → see GLOSSARY.md
- **Device** → see GLOSSARY.md
- **Policy** → see GLOSSARY.md
- **TrustedDevice** → see GLOSSARY.md
- **UnlockTimer** → see GLOSSARY.md
- **ActivityLog** → see GLOSSARY.md

---

## References

- **GLOSSARY.md** — canonical domain vocabulary
- **ARCHITECTURE.md** — system design and boundaries
- **SECURITY.md** — security posture and enforcement
