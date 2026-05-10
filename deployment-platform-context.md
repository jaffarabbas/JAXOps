# IIS Management & Deployment Platform — Business & Operational Context

## Business Purpose

This platform eliminates manual, error-prone IIS deployment workflows across multiple Windows servers. Today, deployments are performed by developers manually via RDP, xcopy, or scripts — which introduces human error, lacks audit trails, and cannot scale across many environments.

The IIS Management & Deployment Platform replaces this with:
- A centralized web portal visible to the entire DevOps team
- Automated, audited deployments triggered from a browser
- Real-time visibility into server health and deployment progress
- Rollback capability with one click
- A permanent audit trail for compliance

---

## Internal DevOps Workflow

### Before this platform
1. Developer builds application locally
2. Developer connects via RDP to target IIS server
3. Developer manually stops app pool, copies files, starts app pool
4. No centralized log of who deployed what when
5. Rollback requires finding previous files manually

### After this platform
1. Developer (or CI/CD pipeline) uploads deployment zip to portal
2. Portal validates package (hash, format)
3. Operator selects target servers and clicks Deploy
4. Agent on each server executes deployment pipeline automatically
5. Operator watches live logs in browser
6. Deployment history and audit trail auto-recorded
7. On failure: one-click rollback restores previous backup

---

## IIS Automation Goals

- **Zero-RDP deployments**: Never need to log into a server to deploy
- **Consistent process**: Same 13-step pipeline executed identically on every server
- **Parallel multi-server**: Deploy to staging and production simultaneously or sequentially
- **Safe deployment**: App goes offline gracefully (app_offline.htm) before file replacement
- **Health validation**: Agent checks website health after deploy before reporting success
- **Backup-first**: Agent always creates backup before overwriting — enables fast rollback

---

## Deployment Management Goals

- Track every deployment: who, what version, which servers, when, outcome
- Support scheduled deployments (deploy at off-peak hours)
- Support deployment locking (prevent concurrent deploys to same site)
- Support environment-based deployment gates (must deploy to staging before production)
- Provide rollback to any previous successful deployment
- Store deployment packages for at least 30 days for rollback availability

---

## Multi-Server Architecture Goals

- Support any number of IIS servers (tested for up to 50 servers)
- Each server runs a lightweight Agent (~10MB memory footprint)
- Agent auto-reconnects if portal restarts — no data loss
- Servers organized by environment (Development, Staging, Production) and group
- Dashboard shows all server health at a glance (green/yellow/red status cards)
- IIS site and app pool lists are synchronized from agent on demand

---

## Real-Time Monitoring Goals

- Dashboard refreshes without page reload (SignalR push)
- CPU, RAM, disk usage streamed every 30 seconds per server
- IIS site status (Running, Stopped, Unknown) updated on heartbeat
- Deployment log lines appear in browser as agent streams them (no polling)
- Notifications appear in top bar: "Deployment to PROD-WEB-01 succeeded"

---

## Enterprise Operational Requirements

### High Availability
- Portal can be deployed behind IIS ARR (Application Request Routing) load balancer
- SignalR uses sticky sessions (ARR affinity) or Redis backplane for scale-out
- SQL Server uses Always On Availability Groups for HA
- Agent automatically retries connection (exponential backoff, max 30s)

### Disaster Recovery
- Portal is stateless — can be redeployed from package
- All state in SQL Server (deployments, servers, users, audit logs)
- Agent configuration in local `appsettings.json` — can be scripted/automated

### Observability
- Serilog writes structured JSON logs
- Correlation ID on every request (portal and agent)
- Deployment events traceable end-to-end via `CorrelationId` (Guid)
- Health check endpoint at `/health` (portal) for load balancer probes

### Operational Runbook
- Install agent: `sc create IISManagerAgent binpath= "..." start= auto`
- Register server in portal: Admin → Servers → Add Server (enter hostname + paste API key)
- Rotate agent key: Admin → Server detail → Regenerate Key (agent reconnects automatically)
- View deployment history: Deployments → filter by server/app/date
- Rollback: Deployments → click failed deployment → Rollback

---

## Security Requirements

| Requirement | Implementation |
|-------------|----------------|
| Portal authentication | Windows Authentication (Negotiate) — no credentials managed by this app |
| Portal authorization | Role-based (AD groups: IISManager-Admins, IISManager-Operators, IISManager-Viewers) |
| Agent authentication | Pre-shared API key per server (SHA256 hashed in DB) |
| Transport security | HTTPS/WSS only; HTTP redirected to HTTPS |
| Package integrity | SHA256 hash verified before extraction |
| Directory access | Agent writes only to paths in `AllowedDeployPaths` whitelist |
| No arbitrary execution | Agent accepts only typed command objects — no PowerShell passthrough |
| Audit trail | Every state-changing action recorded in `AuditLogs` with user + timestamp |
| Secrets management | Agent API keys and DB connection strings managed via environment variables or Windows DPAPI |

---

## Scalability Expectations

| Dimension | Initial target | Scale ceiling |
|-----------|---------------|---------------|
| Managed servers | 5–15 | 50+ |
| Concurrent deployments | 3–5 | 20 (with queue) |
| Deployment log lines/day | ~10,000 | 1M (archive old logs) |
| Browser users (concurrent) | 5–10 | 100 (SignalR scales via Redis backplane) |
| Deployment history retention | 30 days default | Configurable, archive to cold storage |

---

## Out of Scope (MVP)

- Kubernetes / Docker deployment (Windows-only IIS scope)
- CI/CD pipeline integration triggers via API (planned V2)
- Blue/green deployment (planned V2)
- Automated SSL certificate management (manual for MVP)
- Mobile-responsive portal (desktop-first for MVP)
- Multi-tenancy (single organization for MVP)

---

## Technology Decisions and Rationale

| Decision | Choice | Rationale |
|----------|--------|-----------|
| ORM | Dapper | Full SQL control, no migrations needed for MVP, team familiar with raw SQL |
| Auth | Windows Auth | Already on AD-integrated internal network; no password management overhead |
| Real-time | SignalR | Native .NET support, WebSocket with fallback, built-in group management |
| Agent pattern | SignalR client (not REST) | Agents behind NAT/firewall — agent initiates outbound connection; portal pushes commands |
| IIS control | Microsoft.Web.Administration | Official IIS API; no PowerShell dependency |
| Frontend framework | Bootstrap 5 + vanilla JS | No SPA complexity; Razor Pages renders server-side; JS only for SignalR and UI interactions |
| Database | SQL Server | Enterprise standard already in environment; Dapper + SQL Server is well-understood combination |
