# IIS Management & Deployment Platform — Architecture Reference

## Purpose
This document describes the architecture, conventions, and design decisions for the **IIS Management & Deployment Platform** — an internal enterprise DevOps portal built with ASP.NET Core 8 Razor Pages. It exists alongside the existing `DeploymentTool` WPF project inside the same solution but is entirely independent of it.

---

## Solution Layout

```
JAXOps.sln
├── DeploymentTool/                    ← EXISTING WPF app — do NOT modify
├── DeploymentTool.Installer/          ← EXISTING installer — do NOT modify
└── src/
    ├── IISManager.Domain/             ← Domain entities, enums, repository interfaces
    ├── IISManager.Contracts/          ← Shared DTOs between Portal ↔ Agent (SignalR commands/events)
    ├── IISManager.Shared/             ← Constants, Result<T>, extension methods
    ├── IISManager.Application/        ← Application services, DTOs, validators, interfaces
    ├── IISManager.Infrastructure/     ← Dapper repositories, SQL Server, external services
    ├── IISManager.WebPortal/          ← ASP.NET Core 8 Razor Pages web portal
    └── IISManager.AgentService/       ← .NET 8 Worker Service (runs as Windows Service)
```

**Rule:** Projects in `src/` never reference `DeploymentTool/`. The WPF project never references anything in `src/`.

---

## High-Level Architecture

```
Browser (Bootstrap 5 + SignalR JS)
       ↕  HTTPS / WebSockets
IISManager.WebPortal  (ASP.NET Core 8)
  ├── DeploymentHub    ← Browser clients subscribe to deployment events
  ├── MonitoringHub    ← Browser clients subscribe to health/status events
  └── AgentHub         ← Agent services connect here as clients
       ↕  SignalR (persistent WebSocket)
IISManager.AgentService  (Windows Service on each IIS server)
  ├── IISManagementService     ← Microsoft.Web.Administration wrapper
  ├── DeploymentExecutor       ← Full deployment pipeline
  ├── BackupService            ← Backup before deploy
  ├── HealthReporter           ← CPU/RAM/disk/IIS status
  └── CommandDispatcher        ← Routes incoming commands to handlers
       ↕
Local IIS / File System
```

---

## SignalR Architecture

### Hubs (hosted in WebPortal)

| Hub | Client type | Direction | Purpose |
|-----|-------------|-----------|---------|
| `AgentHub` | AgentService | Agent → Portal | Heartbeats, health data, deployment progress, command results |
| `DeploymentHub` | Browser | Portal → Browser | Live deployment logs, progress percentage, status changes |
| `MonitoringHub` | Browser | Portal → Browser | Server health cards, CPU/RAM/disk, IIS site status |

### Agent Authentication
- Agent sends `AgentApiKey` in SignalR connection header
- `AgentHub` validates key against `Servers.AgentApiKey` in SQL Server
- On connect: stores `ConnectionId` → server mapping in memory (`IAgentConnectionRegistry`)
- On disconnect: marks server as offline, broadcasts to monitoring hub

### Command Flow (Portal → Agent)
```
1. User clicks "Deploy" in browser
2. Razor Page handler calls IDeploymentAppService.StartDeploymentAsync()
3. Application service creates Deployment record (status=Queued)
4. Application service calls IAgentCommunicationService.SendCommandAsync(serverId, command)
5. IAgentCommunicationService resolves serverId → connectionId via IAgentConnectionRegistry
6. Calls IHubContext<AgentHub>.Clients.Client(connectionId).SendAsync("ExecuteCommand", command)
7. Agent receives command, dispatches to appropriate ICommandHandler
8. Agent streams progress events back: hubConnection.InvokeAsync("ReportProgress", event)
9. AgentHub.ReportProgress() updates DB + broadcasts to DeploymentHub group
10. Browser receives real-time log lines via SignalR
```

---

## Database Schema Design

Uses **SQL Server** with **Dapper**. No EF. All queries are raw SQL stored in repository classes or as inline strings in private const fields.

### Key tables

| Table | Purpose |
|-------|---------|
| `Servers` | Registered IIS servers with agent API key |
| `Applications` | Logical apps (mapped to a server + website) |
| `Deployments` | Deployment records with status, version, timestamps |
| `DeploymentTargets` | Which servers/sites each deployment targets |
| `DeploymentLogs` | Per-line log output from agent during deployment |
| `Websites` | Cached IIS site list (refreshed on agent sync) |
| `ApplicationPools` | Cached IIS app pool list |
| `Users` | Windows auth users with display name and role |
| `Roles` | Admin, Operator, Viewer |
| `UserRoles` | Many-to-many |
| `AuditLogs` | Immutable audit trail for all critical operations |
| `ServerHealth` | Time-series health snapshots (CPU, RAM, disk) |
| `Notifications` | Per-user notification queue |
| `DeploymentPackages` | Uploaded zip package metadata (path, hash, size) |

### Repository pattern
- Each aggregate root has a dedicated repository interface in `IISManager.Domain/Interfaces/`
- Implementations live in `IISManager.Infrastructure/Repositories/`
- All methods are `async Task<T>`
- `IUnitOfWork` wraps `IDbTransaction` for multi-step operations
- `DatabaseFactory` creates `IDbConnection` from config connection string

---

## Agent Communication Protocol

### Commands (Portal → Agent)
All commands inherit `AgentCommandBase`:
```json
{
  "CommandId": "guid",
  "CommandType": "DeployApplication",
  "ServerId": 1,
  "IssuedAt": "2026-05-10T10:00:00Z",
  "Payload": { ... command-specific fields ... }
}
```

### Events (Agent → Portal)
All events inherit `AgentEventBase`:
```json
{
  "EventId": "guid",
  "EventType": "DeploymentProgress",
  "ServerId": 1,
  "DeploymentId": 42,
  "Timestamp": "2026-05-10T10:00:01Z",
  "Payload": { ... event-specific fields ... }
}
```

### Heartbeat
Agent sends heartbeat every 30 seconds:
```json
{
  "ServerId": 1,
  "MachineName": "PROD-WEB-01",
  "AgentVersion": "1.0.0",
  "CpuPercent": 12.5,
  "RamUsedMB": 3400,
  "RamTotalMB": 16384,
  "DiskUsedGB": 120,
  "DiskTotalGB": 500,
  "IISRunning": true,
  "Timestamp": "2026-05-10T10:00:00Z"
}
```

---

## Deployment Pipeline (Agent-side)

```
1.  ValidatePackage()          → verify zip integrity + SHA256 hash
2.  AcquireDeploymentLock()    → prevent concurrent deploys to same site
3.  BackupCurrentApp()         → zip current physical path → backup folder
4.  StopApplicationPool()      → Microsoft.Web.Administration
5.  WriteAppOffline()          → drop app_offline.htm at physical path
6.  ExtractPackage()           → unzip to temp folder
7.  ReplaceFiles()             → atomic copy from temp to physical path
8.  ApplyConfigTransform()     → merge appsettings overrides if provided
9.  RemoveAppOffline()         → delete app_offline.htm
10. StartApplicationPool()     → Microsoft.Web.Administration
11. ValidateWebsite()          → HTTP GET health check with retry (3×, 10s delay)
12. ReleaseDeploymentLock()
13. ReportResult()             → send DeploymentCompletedEvent to portal
```

Each step streams a log line back to the portal. On any failure: stop, rollback (restore backup), report failure.

---

## IIS Management Service (Agent-side)

Uses `Microsoft.Web.Administration.ServerManager` for all IIS operations. The `ServerManager` must be instantiated with elevated privileges (agent runs as Local System or Admin service account).

Key service boundary:
- `ISiteManagementService` — CRUD + start/stop/restart websites
- `IAppPoolManagementService` — CRUD + start/stop/recycle app pools
- `IBindingManagementService` — HTTP/HTTPS binding management
- `IIISStatusService` — read-only status for health reporting

---

## Security Architecture

- **Transport**: HTTPS only. Agent connects to Portal via wss:// (WSS over TLS).
- **Agent authentication**: Pre-shared API key per server, stored hashed (SHA256) in DB.
- **Portal authentication**: Windows Authentication (IWA) via Negotiate/Kerberos.
- **Authorization**: Policy-based. Roles: `Admin`, `Operator`, `Viewer`.
  - `Admin`: full CRUD, delete, rollback
  - `Operator`: deploy, restart, start/stop
  - `Viewer`: read-only dashboard
- **Package validation**: SHA256 hash verified before extraction. Only `.zip` allowed.
- **Directory restriction**: Agent only writes to whitelisted physical paths configured in `appsettings.json`.
- **No arbitrary command execution**: Agent accepts only typed command objects. No PowerShell passthrough.
- **Audit trail**: Every state-changing operation writes to `AuditLogs` with user, timestamp, before/after state.

---

## Coding Conventions

### Naming
- Classes: PascalCase
- Interfaces: `I` prefix PascalCase
- Private fields: `_camelCase`
- Async methods: `*Async` suffix
- Repository methods: `GetByIdAsync`, `GetAllAsync`, `InsertAsync`, `UpdateAsync`, `DeleteAsync`

### Error handling
- All service methods return `Result<T>` (custom) — never throw across layer boundaries
- Repository methods throw on DB errors (let the caller decide)
- Agent command handlers catch exceptions, return `CommandResult` with `Success=false`

### Logging
- Use `ILogger<T>` everywhere via DI
- Serilog sinks: file (rolling daily), console (structured JSON in production)
- Correlation ID attached via middleware (`X-Correlation-Id` header)
- Agent uses same Serilog setup, writes to local log file + sends critical events to portal

### Dependency Injection
- `IServiceCollection` extension methods for each layer (`AddDomain`, `AddApplication`, `AddInfrastructure`, `AddWebPortal`)
- Agent has its own `AddAgentServices` extension
- Never use service locator pattern

---

## Development Workflow

1. Start SQL Server, apply `DatabaseSchema.sql` (idempotent — uses `IF NOT EXISTS`)
2. Run `IISManager.WebPortal` (HTTPS on port 7000)
3. Run `IISManager.AgentService` locally (connects to portal at `https://localhost:7000/hubs/agent`)
4. Agent registers using API key from its `appsettings.json` → `AgentApiKey`
5. Dashboard shows server online within 30 seconds (first heartbeat)

---

## Production Deployment Recommendations

- Portal: Deploy to a dedicated management server (not a production IIS server)
- Portal IIS site: Require Windows Auth, disable anonymous, set App Pool to No Managed Code
- Agent: Install via `sc create` or NSSM, set to auto-start, run as Local System
- Database: Use SQL Server Always On for HA; portal uses read/write connection string
- TLS: Use internal PKI certificate on portal; agent validates cert thumbprint in config
- Network: Portal and agents on internal network only; never expose portal externally
- Backups: Configure backup path to network share or separate disk from app files
