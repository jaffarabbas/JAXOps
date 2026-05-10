# IIS Manager Platform

A centralised web portal for managing and deploying applications across multiple Windows IIS servers. No RDP. No xcopy. Full audit trail, real-time logs, rollback on demand.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Solution Structure](#2-solution-structure)
3. [Prerequisites](#3-prerequisites)
4. [Database Setup](#4-database-setup)
5. [Portal Deployment](#5-portal-deployment)
6. [Active Directory Setup](#6-active-directory-setup)
7. [Agent Deployment](#7-agent-deployment)
8. [Registering a Server in the Portal](#8-registering-a-server-in-the-portal)
9. [Verifying the Connection](#9-verifying-the-connection)
10. [Configuration Reference](#10-configuration-reference)
11. [How Deployments Work](#11-how-deployments-work)
12. [Troubleshooting](#12-troubleshooting)
13. [Security Notes](#13-security-notes)

---

## 1. Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│  Browser  (HTTPS + SignalR WebSocket)                │
│  Real-time logs, health metrics, deployment status   │
└────────────────────┬─────────────────────────────────┘
                     │ Windows Auth (Kerberos/NTLM)
         ┌───────────▼───────────┐
         │   IISManager.Portal   │  ASP.NET Core 8 · Razor Pages
         │                       │
         │  ┌─ AgentHub          │  ← Agents connect here (API key auth)
         │  ├─ DeploymentHub     │  ← Browser subscribes to live deploy logs
         │  └─ MonitoringHub     │  ← Browser subscribes to health / status
         └───────────┬───────────┘
                     │
         ┌───────────▼───────────┐
         │     SQL Server        │  Servers · Deployments · Packages
         │                       │  AuditLogs · Health snapshots
         └───────────┬───────────┘
                     │ SignalR (HTTPS WebSocket)
                     │ Headers: X-Agent-ApiKey · X-Server-Id
         ┌───────────▼───────────┐
         │  IISManager.Agent     │  .NET 8 Windows Service
         │  (one per IIS server) │
         │                       │
         │  · Heartbeat every 30s│
         │  · Execute commands   │
         │  · Stream deploy logs │
         │  · Manage IIS & files │
         └───────────┬───────────┘
                     │
         ┌───────────▼───────────┐
         │   Local IIS / FS      │  Websites · App Pools · Files
         └───────────────────────┘
```

The **portal** never touches the IIS server directly. Every action goes through the **agent** over a persistent SignalR connection. The agent holds its own API key; the portal stores only the SHA-256 hash.

---

## 2. Solution Structure

```
src/
├── IISManager.Domain/          Domain entities, enums, repository interfaces
├── IISManager.Contracts/       Commands & events shared by portal and agent
├── IISManager.Shared/          SignalR constants, helper methods
├── IISManager.Application/     Business logic, app services, FluentValidation
├── IISManager.Infrastructure/  Dapper repositories, SQL Server, DB schema
├── IISManager.WebPortal/       ASP.NET Core Razor Pages portal
└── IISManager.AgentService/    .NET 8 Windows Service (deploy to each IIS box)
```

---

## 3. Prerequisites

### Portal server

| Requirement | Version |
|-------------|---------|
| Windows Server | 2019 or later |
| IIS | 10.0 or later |
| .NET Runtime | 8.0 |
| SQL Server | 2019 or later (local or remote) |
| SSL certificate | Valid cert or self-signed (HTTPS required for WebSocket) |

### Each IIS server (agent)

| Requirement | Notes |
|-------------|-------|
| Windows Server | 2016 or later |
| IIS installed | With Management Service feature |
| .NET 8 Runtime | Windows x64 build |
| Network access to portal | TCP 443 / 7000 (HTTPS + WebSocket) |
| Local Administrator | Required to manage IIS and files |

---

## 4. Database Setup

### 4.1 Create the database

```sql
CREATE DATABASE IISManagerDb
    COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
```

### 4.2 Create a dedicated SQL user (least privilege)

```sql
USE IISManagerDb;
GO

CREATE LOGIN iismanager_app WITH PASSWORD = 'StrongPassword123!';
CREATE USER  iismanager_app FOR LOGIN iismanager_app;

-- Grant only what the portal needs
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO iismanager_app;
GO
```

### 4.3 Run the schema script

Execute the full contents of:

```
src/IISManager.Infrastructure/Schema/DatabaseSchema.sql
```

The script is **idempotent** — safe to run multiple times. It creates 11 tables:

| Table | Purpose |
|-------|---------|
| `Servers` | Registered IIS servers, agent status, heartbeat, API key hash |
| `Applications` | Logical apps (name, default site/pool/path) |
| `DeploymentPackages` | Uploaded ZIP packages with SHA-256 hash |
| `Deployments` | Deployment records (version, status, who, when) |
| `DeploymentTargets` | Which server/site each deployment targets |
| `DeploymentLogs` | Streamed log lines from the agent |
| `Websites` | Cached IIS site list (synced from agent) |
| `ApplicationPools` | Cached app pool list (synced from agent) |
| `ServerHealth` | Time-series CPU / RAM / disk snapshots |
| `AuditLogs` | Immutable audit trail of every action |
| `Notifications` | Per-user notification queue |

---

## 5. Portal Deployment

### 5.1 Publish

```powershell
dotnet publish src\IISManager.WebPortal\IISManager.WebPortal.csproj `
    -c Release `
    -o C:\Deploy\IISManagerPortal
```

### 5.2 Configure appsettings.json

Edit `C:\Deploy\IISManagerPortal\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver.internal,1433;Database=IISManagerDb;User Id=iismanager_app;Password=StrongPassword123!;TrustServerCertificate=True;"
  },
  "Portal": {
    "PackageStorePath": "C:\\IISManagerPackages",
    "MaxPackageSizeMB": 500,
    "HealthSnapshotRetentionDays": 30
  }
}
```

> **PackageStorePath** — agents download deployment ZIPs from the portal over HTTPS. This folder must be readable by the portal's app pool identity and have enough disk space for all packages.

### 5.3 Create an IIS site for the portal

```powershell
# Create app pool
New-WebAppPool -Name "IISManagerPortal"
Set-ItemProperty IIS:\AppPools\IISManagerPortal -Name processModel.identityType -Value NetworkService

# Create site (adjust port / cert thumbprint)
New-WebSite -Name "IISManagerPortal" `
    -PhysicalPath "C:\Deploy\IISManagerPortal" `
    -ApplicationPool "IISManagerPortal" `
    -Port 443 `
    -Ssl
```

### 5.4 Enable Windows Authentication on the portal site

```powershell
Set-WebConfigurationProperty `
    -Filter "system.webServer/security/authentication/windowsAuthentication" `
    -Name "enabled" -Value $true `
    -PSPath "IIS:\Sites\IISManagerPortal"

Set-WebConfigurationProperty `
    -Filter "system.webServer/security/authentication/anonymousAuthentication" `
    -Name "enabled" -Value $false `
    -PSPath "IIS:\Sites\IISManagerPortal"
```

### 5.5 Grant folder permissions

```powershell
# App pool identity needs write access to the package store
icacls "C:\IISManagerPackages" /grant "IIS AppPool\IISManagerPortal:(OI)(CI)M"

# Log folder
icacls "C:\Deploy\IISManagerPortal\logs" /grant "IIS AppPool\IISManagerPortal:(OI)(CI)M"
```

### 5.6 Verify the portal is running

Browse to `https://<portal-hostname>/health` — should return `Healthy`.

---

## 6. Active Directory Setup

The portal uses **Windows Authentication**. Access is controlled by AD security group membership.

### 6.1 Create three AD security groups

| Group name | Access level |
|------------|-------------|
| `IISManager-Admins` | Full access — server registration, all deployments, audit logs |
| `IISManager-Operators` | Deploy applications, start/stop sites and app pools |
| `IISManager-Viewers` | Read-only — dashboard, deployment history, audit logs |

```powershell
# Run on a domain controller (or use AD Users and Computers)
New-ADGroup -Name "IISManager-Admins"    -GroupScope Global -GroupCategory Security
New-ADGroup -Name "IISManager-Operators" -GroupScope Global -GroupCategory Security
New-ADGroup -Name "IISManager-Viewers"   -GroupScope Global -GroupCategory Security
```

### 6.2 Add users to the groups

```powershell
Add-ADGroupMember -Identity "IISManager-Admins"    -Members "alice","bob"
Add-ADGroupMember -Identity "IISManager-Operators" -Members "charlie","diana"
Add-ADGroupMember -Identity "IISManager-Viewers"   -Members "eve"
```

> Users who belong to none of these groups will be authenticated but denied access (403).  
> In **development mode** (`ASPNETCORE_ENVIRONMENT=Development`) role checks are skipped — any domain user can access all pages.

---

## 7. Agent Deployment

Repeat these steps for **every** IIS server you want to manage.

### 7.1 Publish the agent (Windows x64)

```powershell
dotnet publish src\IISManager.AgentService\IISManager.AgentService.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o C:\Deploy\IISManagerAgent
```

### 7.2 Copy to the target server

```powershell
# From your build machine
$dest = "\\PROD-WEB-01\C$\Program Files\IISManagerAgent"
New-Item -Path $dest -ItemType Directory -Force
Copy-Item -Path "C:\Deploy\IISManagerAgent\*" -Destination $dest -Recurse -Force
```

### 7.3 Generate an API key for this server

The API key is a secret string that the agent sends to the portal on every connection. It must be a long random string — treat it like a password.

```powershell
# Recommended: generate a cryptographically random key
[System.Convert]::ToBase64String((1..32 | ForEach-Object { [byte](Get-Random -Max 256) }))
# Example output: xK9mP2vQrT8nL5jH3dF6sA1wE4oU7iY0bC...
```

**Keep this key — you will paste it into both the agent config and the portal.**

### 7.4 Configure appsettings.json on the target server

Create or edit `C:\Program Files\IISManagerAgent\appsettings.json`:

```json
{
  "Agent": {
    "ServerId": 1,
    "ApiKey": "PASTE_YOUR_GENERATED_KEY_HERE",
    "PortalUrl": "https://iismanager.internal",
    "AllowedDeployPaths": [
      "C:\\inetpub\\wwwroot",
      "D:\\Apps"
    ],
    "BackupBasePath": "C:\\IISManagerBackups",
    "MaxConcurrentDeployments": 2,
    "AllowInsecureSsl": false
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\Logs\\IISManagerAgent\\agent-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

> **ServerId** must match the ID assigned when you register the server in the portal (step 8). If you register via the portal UI first, the portal assigns an ID — update this field before starting the service.

> **AllowedDeployPaths** is a security whitelist. The agent will refuse to deploy to any path not listed here.

### 7.5 Install as a Windows Service

Run on the target server as Administrator:

```powershell
$binPath = '"C:\Program Files\IISManagerAgent\IISManager.AgentService.exe"'

sc.exe create IISManagerAgent `
    binPath= $binPath `
    DisplayName= "IIS Manager Agent" `
    start= auto

sc.exe description IISManagerAgent "Connects this server to the IIS Manager portal"

# Grant Log on as a service right (or run as Local System)
sc.exe config IISManagerAgent obj= LocalSystem

sc.exe start IISManagerAgent
```

### 7.6 Verify the service started

```powershell
Get-Service IISManagerAgent
# Should show: Running

# Check recent log lines
Get-Content "C:\Logs\IISManagerAgent\agent-$(Get-Date -Format 'yyyyMMdd').log" -Tail 30
```

A successful startup looks like:

```
[INF] IIS Manager Agent starting...
[INF] Connecting to portal: https://iismanager.internal/hubs/agent
[INF] Agent connected: Server 1 (PROD-WEB-01), ConnectionId=abc123
[INF] Initial IIS state reported: 3 sites, 4 pools
[INF] Heartbeat loop started (interval: 30s)
```

---

## 8. Registering a Server in the Portal

### Option A — Portal UI (recommended)

1. Browse to the portal → **Servers** → **Add Server**
2. Fill in:
   - **Server Name** — display name, e.g. `PROD-WEB-01`
   - **Hostname** — FQDN, e.g. `prod-web-01.internal`
   - **IP Address** — e.g. `10.0.1.50`
   - **Environment** — `Production` / `Staging` / `UAT` / `Development`
   - **Group** — optional grouping label, e.g. `Web Tier`
3. Click **Register Server**
4. The portal shows the **API key** once — copy it immediately
5. Paste the key into the agent's `appsettings.json` → `Agent.ApiKey`
6. Note the **Server ID** shown in the server list — set `Agent.ServerId` to this value
7. Restart the agent service: `Restart-Service IISManagerAgent`

### Option B — SQL (scripted / batch registration)

```sql
-- Generate key beforehand (PowerShell step 7.3)
-- Hash it first:
-- DECLARE @hash NVARCHAR(64) = CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', 'YOUR_PLAINTEXT_KEY'), 2)

DECLARE @plainKey NVARCHAR(200) = 'YOUR_PLAINTEXT_KEY';
DECLARE @hash NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', @plainKey), 2));

INSERT INTO Servers
    (Name, Hostname, IpAddress, Environment, [Group], Description,
     AgentApiKeyHash, Status, IsActive, CreatedAt, CreatedBy)
VALUES
    ('PROD-WEB-01', 'prod-web-01.internal', '10.0.1.50',
     'Production', 'Web Tier', NULL,
     @hash, 'Unknown', 1, GETUTCDATE(), 'devops-team');

-- Take note of the inserted ID: @@IDENTITY
SELECT @@IDENTITY AS ServerId;
```

Set `Agent.ServerId` to the returned ID and `Agent.ApiKey` to the plaintext key.

---

## 9. Verifying the Connection

After registering the server and (re)starting the agent service:

1. **Portal → Servers** — the server card should flip to **Online** (green) within 30 seconds
2. **Portal → Servers → [Server Name]** — you should see CPU %, RAM, and disk readings updating live
3. Click **Sync IIS State** — the Websites and App Pools tabs should populate
4. The **Last Heartbeat** timestamp updates every 30 seconds

If the server stays **Offline**, see [Troubleshooting](#12-troubleshooting).

---

## 10. Configuration Reference

### Portal — appsettings.json

| Key | Type | Example | Description |
|-----|------|---------|-------------|
| `ConnectionStrings.DefaultConnection` | string | SQL Server connection string | All portal data |
| `Portal.PackageStorePath` | string | `C:\IISManagerPackages` | Where uploaded ZIPs are stored |
| `Portal.MaxPackageSizeMB` | int | `500` | Max upload size in MB |
| `Portal.HealthSnapshotRetentionDays` | int | `30` | Purge health data older than N days |

### Agent — appsettings.json

| Key | Type | Example | Description |
|-----|------|---------|-------------|
| `Agent.ServerId` | int | `1` | Must match the server's ID in the DB |
| `Agent.ApiKey` | string | `"xK9mP2vQ..."` | Plaintext key — portal stores only the hash |
| `Agent.PortalUrl` | string | `"https://iismanager.internal"` | Portal base URL (no trailing slash) |
| `Agent.AllowedDeployPaths` | string[] | `["C:\\inetpub\\wwwroot"]` | Security whitelist for deploy targets |
| `Agent.BackupBasePath` | string | `"C:\\IISManagerBackups"` | Where pre-deploy backups are stored |
| `Agent.MaxConcurrentDeployments` | int | `2` | Max simultaneous deployments on this server |
| `Agent.AllowInsecureSsl` | bool | `false` | Set `true` only for self-signed certs in dev |

### SignalR connection headers sent by agent

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Agent-ApiKey` | Plaintext API key | Authenticates the agent |
| `X-Server-Id` | Server ID as string | Identifies which server is connecting |

---

## 11. How Deployments Work

### Upload a package

1. Portal → **Deployments → New Deployment**
2. Click **+** next to Package → upload a `.zip` containing your built application
3. Enter version string (e.g. `2.1.4`)
4. Portal stores the ZIP on disk and records SHA-256 hash

### Start a deployment

1. Select **Application** and **Package**
2. Add one or more **Targets** (server + IIS site name + app pool + physical path)
3. Choose mode:
   - **Sequential** — one server at a time (safer for rolling deploys)
   - **Parallel** — all servers simultaneously (faster, all-or-nothing)
4. Click **Start Deployment**

### What happens on the agent (13-step pipeline)

```
1  Validate physical path is in AllowedDeployPaths
2  Acquire lock file (prevent concurrent deploys to same site)
3  Download ZIP from portal over HTTPS
4  Validate SHA-256 hash
5  Extract ZIP to temp folder
6  Stop IIS website
7  Backup current files to BackupBasePath
8  Replace files (delete old, copy new)
9  Start IIS website
10 HTTP health check (3 retries, 10 s delay)
11 Report success or failure to portal
12 Release lock file
13 Stream all log lines to portal in real time
```

The browser's **Console** page shows every log line as it arrives via SignalR.

### Rollback

Portal → Deployments → find the deployment → click **Rollback**.  
The agent restores files from the backup created in step 7 above.

### Real-time IIS sync

After any action (start/stop site, recycle pool, deploy), the portal automatically:
1. Waits for `OnCommandResult` from the agent via SignalR
2. Triggers `SyncIISState` to refresh the cached site/pool data
3. Reloads the page when `OnIISStateUpdate` confirms the data is written

---

## 12. Troubleshooting

### Agent won't connect / server stays Offline

**Step 1 — Can the agent server reach the portal?**
```powershell
Test-NetConnection -ComputerName iismanager.internal -Port 443
# TcpTestSucceeded should be True
```

**Step 2 — Is the service running?**
```powershell
Get-Service IISManagerAgent
```

**Step 3 — Check agent logs**
```powershell
Get-Content "C:\Logs\IISManagerAgent\agent-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50
```

Common log errors and fixes:

| Error | Fix |
|-------|-----|
| `Connection refused` | Portal not running or wrong port in `PortalUrl` |
| `SSL certificate error` | Set `AllowInsecureSsl: true` (dev only) or install valid cert |
| `Agent connection rejected — invalid API key` | Key mismatch — re-register server in portal and update `ApiKey` |
| `Agent connection rejected — missing API key` | `ApiKey` is empty in `appsettings.json` |
| `ServerId mismatch` | `ServerId` in config does not match the DB record |

**Step 4 — Restart the service after any config change**
```powershell
Restart-Service IISManagerAgent
```

### Deployment fails

**Portal → Deployments → [deployment] → Console** shows detailed logs.

Common causes:

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Path not allowed` | Physical path not in `AllowedDeployPaths` | Add path to agent config and restart service |
| `Lock file exists` | Another deploy is running or a previous one crashed | Delete `C:\IISManagerLocks\<sitename>.lock` manually |
| `Hash mismatch` | ZIP corrupted during download | Re-upload package |
| `Site failed health check` | App crashed after deploy | Check IIS logs; rollback if needed |
| `Package download failed` | Agent can't reach portal | Check network; check portal is running |

### IIS state not updating / Sync required twice

This was a known bug (fixed). The Sync button now uses AJAX — the page stays alive and auto-reloads the moment the agent reports back via SignalR. If you still see stale data, check that the agent's SignalR connection is alive (server should show Online in portal).

### Sync loop (sync firing repeatedly)

This was a known bug (fixed). The `OnCommandResult` handler now uses an allowlist of IIS action command types and ignores results from `SyncIISState` itself, preventing the loop.

---

## 13. Security Notes

**API keys**
- Generate a unique key per server (minimum 32 random characters)
- The agent stores the plaintext key locally; the portal stores only the SHA-256 hash
- If a key is compromised, regenerate it in the portal (Servers → [server] → Regenerate Key) — the agent will disconnect and reconnect once you update its config

**Windows Authentication**
- All portal users must authenticate via the domain (Kerberos or NTLM)
- Access is gated by AD group membership — see [Section 6](#6-active-directory-setup)
- Not suitable for internet-facing portals without a reverse proxy handling auth

**Deployment paths**
- The `AllowedDeployPaths` list is a hard security boundary on each agent
- Never include system folders (`C:\Windows`, `C:\Program Files\IISManagerAgent`, etc.)
- The agent validates the physical path on every deploy command before touching any files

**HTTPS / TLS**
- The portal must run HTTPS — browsers cannot upgrade HTTP to WebSocket from an HTTPS page
- Agents connect over the same HTTPS endpoint; set `AllowInsecureSsl: true` only in dev
- Use a valid certificate (Let's Encrypt, internal CA, or commercial) in production

**Database**
- Use the dedicated `iismanager_app` SQL login — not `sa`
- Enable encrypted SQL connections (`Encrypt=True` in the connection string) if SQL Server is remote
- The `AuditLogs` table is insert-only; do not grant UPDATE or DELETE on it to the app user

---

## Quick-Start Checklist

```
□ SQL Server database created and schema applied
□ Portal published and IIS site created with HTTPS
□ Windows Auth enabled on portal IIS site
□ AD groups created (Admins / Operators / Viewers) and users added
□ Portal appsettings.json configured (connection string, package path)
□ Portal health check returns 200: https://<portal>/health

For each IIS server:
□ .NET 8 Runtime installed
□ Agent published and copied to server
□ API key generated (long random string)
□ Server registered in portal (via UI or SQL) — note the ServerId
□ Agent appsettings.json configured (ServerId, ApiKey, PortalUrl, AllowedDeployPaths)
□ Windows Service installed and started
□ Server shows Online in portal within 30 seconds
□ IIS Sync completes and shows sites / app pools
```
