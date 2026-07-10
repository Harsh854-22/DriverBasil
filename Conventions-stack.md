# CONVENTIONS-STACK.md — SecureDeviceControl Code Conventions

> Concrete companion to CONVENTIONS.md. Authoritative for this project.

---

## 1. Language & Casing

| Language | Vars/funcs | Types/Classes | Constants | Files | Notes |
|----------|-----------|---------------|-----------|-------|-------|
| C#       | camelCase (local) / PascalCase (member) | PascalCase | PascalCase | PascalCase.cs | .NET 8 |

**This project uses:** C# / .NET 8 — PascalCase types and files; camelCase locals and parameters; PascalCase public members.

## 2. Tooling

- **Formatter (style authority):** `dotnet format` (`.editorconfig` at repo root)
- **Linter:** `dotnet build` with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in test projects; analyzers via SDK
- **Type checker:** C# compiler (nullable reference types enabled)
- **Config file locations:** `.editorconfig` (root), `Directory.Build.props` (src/)

## 3. File-internal order

```
using directives (System → Microsoft → third-party → project) → namespace → types/constants → public members → private members
```

## 4. Error model

- **Chosen model:** Exceptions internally; **Result wrapper** (`IpcResponse` with `success`, `errorCode`, `payload`) at IPC boundary
- **Rule:** Domain logic returns explicit results where testability matters; IPC always uses structured error codes from `IpcErrorCode`
- **Imports:** Project references via solution structure; no global usings beyond SDK defaults
- **Branch naming:** `feature/short-desc` · **Commit style:** Conventional Commits (recommended)
