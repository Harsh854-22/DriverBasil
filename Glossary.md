# GLOSSARY.md — SecureDeviceControl Canonical Vocabulary

> **Status:** Mandatory single source of truth for domain terminology.

---

## 3. Domain Terms

| Term | Definition | Code form | Avoid (synonyms) | Relationships |
|------|------------|-----------|------------------|---------------|
| **Administrator** | The local Windows user who installs, configures, and operates SecureDeviceControl. Authenticates via PIN. *Not* a remote or org-wide admin role. | `Administrator` | ~~admin user~~, ~~superuser~~, ~~operator~~ | Initiates sessions; owns Policy changes. |
| **Device** | A PnP hardware instance detected by the service (instance ID, class, friendly name, connection state). MVP scope emphasizes removable storage. | `Device`, `device_id` | ~~peripheral~~, ~~hardware~~ (too vague), ~~drive~~ (when meaning the entity) | Evaluated by PolicyEngine; may become a TrustedDevice. |
| **Policy** | The persisted enforcement rules (default block mode, session timeout, etc.) applied by PolicyEngine. *Not* generic app configuration. | `Policy`, `PolicyMode` | ~~rule set~~, ~~config~~ (when meaning enforcement) | Governs all connected Devices; stored in service SQLite. |
| **TrustedDevice** | A Device explicitly allowed to bypass the default block, keyed by stable hardware identifier. | `TrustedDevice`, `trusted_device_id` | ~~whitelist entry~~, ~~allowed device~~, ~~approved device~~ | Subset of Devices; stored in service SQLite. |
| **UnlockTimer** | A time-bounded relaxation of Policy that temporarily allows new removable storage. Authoritative state lives in the service. | `UnlockTimer`, `UnlockStatus` | ~~grace period~~, ~~temporary unlock~~, ~~maintenance window~~ | Overrides default block until expiry; audited. |
| **ActivityLog** | An append-only audit record of security-relevant events (auth, policy, device actions). *Not* application debug logs. | `ActivityLog`, `ActivityLogEntry` | ~~audit trail~~ (use ActivityLog in code/UI) | Written by service; queried via IPC. |
| **PolicyEngine** | Domain component that evaluates Policy against Devices and produces Enable/Disable decisions with reason codes. | `PolicyEngine` | ~~enforcer~~, ~~guard~~, ~~filter~~ | Uses Policy, TrustedDevice list, UnlockTimer state. |
| **Session** | A short-lived authenticated context issued after successful PIN validation. Required for mutating IPC operations. | `Session`, `sessionToken` | ~~login~~ (verb OK in UI), ~~token~~ alone (ambiguous) | Created by service; held in-memory; expires per Policy. |
| **IpcOperation** | A versioned request type in the Named Pipe protocol (e.g., `ValidatePin`, `ListDevices`). | `IpcOperation`, `IpcRequest` | ~~command~~, ~~message type~~ | Defined in Shared; handled by service. |

---

## 5. Acronyms & Abbreviations

| Acronym | Expansion | Notes |
|---------|-----------|-------|
| **DPAPI** | Data Protection API | Windows API for protecting secrets at rest. |
| **IPC** | Inter-Process Communication | Named Pipe channel between Desktop and Service. |
| **PnP** | Plug and Play | Windows device arrival/removal notifications. |
| **PIN** | Personal Identification Number | 6-digit numeric code; hashed with Argon2id in service only. |

---

## References

- **CONVENTIONS-STACK.md** — casing rules for code forms above
- **PRODUCT.md** — product intent and scope
