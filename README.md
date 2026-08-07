# SecureDeviceControl (Kavach)

> A robust, offline, double-PIN-gated Windows Service and Desktop interface for workstation hardware security.

## What this is
SecureDeviceControl is a native Windows security tool that helps local administrators control USB/removable storage access via PIN-protected policy enforcement. It runs an active enforcement loop under the high-privilege Local System account to ensure default-blocked USB ports cannot be tampered with by standard users.

## Tech Stack
- **Language / Runtime:** C# / .NET 8 (Windows-only)
- **Frameworks:** WPF (Windows Presentation Foundation) for Desktop UI, Windows Service API
- **IPC:** Named Pipes (`SecureDeviceControl.v1`)
- **Database:** SQLite (local audit logs & credentials)
- **Cryptography:** Argon2id PIN hashing, Windows Data Protection API (DPAPI) for encryption at rest
- See [Architecture.md](file:///c:/Users/admin/Desktop/DriverBasil/DriverBasil/Architecture.md) for more details.

## Getting Started

### Prerequisites
- .NET 8.0 SDK (on PATH, or using local compiler path)
- Windows 10 or 11 (x64)

### Build & Test Commands
```powershell
# Restore dependencies and build the solution
dotnet build SecureDeviceControl.sln

# Run all unit tests
dotnet test SecureDeviceControl.sln

# Publish release builds of Service and Desktop apps
powershell -File .\scripts\publish-release.ps1

# Package binaries and deployment scripts into a ZIP archive
powershell -File .\scripts\package-zip.ps1
```

### Installation
Once packaged, deploy to workstations using:
1. Extract `SecureDeviceControl-Release.zip`.
2. Double-click `SecureDeviceControl.Desktop.exe` — the app automatically installs, registers, and starts the background service via standard Windows UAC elevation (**Zero typing required!**).
3. *(Optional Fallback)* Right-click `Install-Service.cmd` and select **"Run as administrator"** to pre-install the background service.

## Project Structure
- `src/`: Main implementation files.
  - `SecureDeviceControl.Service/`: Background Windows Service.
  - `SecureDeviceControl.Desktop/`: User-level WPF administration GUI.
  - `SecureDeviceControl.Domain/`: Core domain types, policies, logging, and validation logic.
  - `SecureDeviceControl.Infrastructure/`: Named Pipe IPC, SQLite repositories, and crypto helper implementations.
  - `SecureDeviceControl.Shared/`: Shared models, constants, and IPC protocols.
- `tests/`: Domain and Service unit tests.
- `scripts/`: Helper scripts for build, installation, uninstallation, and packaging.
- `docs/`: Technical and design documentation.

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
Proprietary in-house tool. All rights reserved.
