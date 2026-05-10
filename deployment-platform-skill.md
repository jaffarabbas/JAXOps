# IIS Management & Deployment Platform — Developer Skills Reference

## .NET 8 Patterns Used

### Worker Service (AgentService)
```csharp
// Program.cs pattern for Windows Service
builder.Services.AddHostedService<AgentWorker>();
builder.Host.UseWindowsService(opts => opts.ServiceName = "IISManagerAgent");
```
- `IHostedService` / `BackgroundService` for long-running work
- `CancellationToken` propagated through all async chains
- `IHostApplicationLifetime` for graceful shutdown coordination

### Result<T> Pattern
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```
Use in application layer to avoid exception-based flow across layer boundaries.

---

## Razor Pages Architecture

### Page organization
```
Pages/
├── Index.cshtml                 ← Dashboard
├── Servers/
│   ├── Index.cshtml             ← Server list
│   ├── Details.cshtml           ← Single server detail + IIS sites
│   └── Create.cshtml
├── IIS/
│   ├── Websites.cshtml          ← Website management for a server
│   └── AppPools.cshtml          ← App pool management for a server
├── Deployments/
│   ├── Index.cshtml             ← Deployment history
│   ├── Create.cshtml            ← New deployment wizard
│   └── Console.cshtml           ← Live deployment console (SignalR)
├── Audit/
│   └── Index.cshtml
└── Shared/
    ├── _Layout.cshtml
    ├── _Sidebar.cshtml
    └── _Notifications.cshtml
```

### Page handler convention
- `OnGetAsync()` — page load, populate `[BindProperty]` view models
- `OnPostAsync()` — form submission
- `OnPostDeployAsync()` — named handler (`asp-page-handler="Deploy"`)
- Return `Page()` on validation failure; `RedirectToPage()` on success

### Binding
```csharp
[BindProperty]
public CreateDeploymentDto Input { get; set; } = new();
```

---

## SignalR Implementation

### Hub definition
```csharp
[Authorize]
public class DeploymentHub : Hub
{
    public async Task SubscribeToDeployment(int deploymentId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"deployment-{deploymentId}");

    public async Task SubscribeToServer(int serverId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"server-{serverId}");
}
```

### Sending from outside a hub (server push)
```csharp
// Inject IHubContext<DeploymentHub>
await _deploymentHub.Clients
    .Group($"deployment-{deploymentId}")
    .SendAsync("OnLogLine", new { Timestamp = DateTime.UtcNow, Message = message });
```

### Agent SignalR client (in AgentService)
```csharp
_hubConnection = new HubConnectionBuilder()
    .WithUrl(portalUrl + "/hubs/agent", opts =>
    {
        opts.Headers.Add("X-Agent-ApiKey", _apiKey);
        opts.Headers.Add("X-Server-Id", _serverId.ToString());
    })
    .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) })
    .Build();

_hubConnection.On<AgentCommandBase>("ExecuteCommand", async cmd =>
    await _dispatcher.DispatchAsync(cmd, CancellationToken.None));
```

### Browser JS (SignalR client)
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/deployment")
    .withAutomaticReconnect()
    .build();

connection.on("OnLogLine", (data) => appendLogLine(data.timestamp, data.message));
connection.on("OnProgress", (data) => updateProgressBar(data.percent));
connection.on("OnStatusChange", (data) => updateStatusBadge(data.status));

await connection.start();
await connection.invoke("SubscribeToDeployment", deploymentId);
```

---

## Dapper Implementation

### Connection factory
```csharp
public class DatabaseFactory : IDatabaseFactory
{
    private readonly string _connectionString;
    public IDbConnection CreateConnection()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
```

### Repository pattern
```csharp
public class ServerRepository : IServerRepository
{
    private readonly IDatabaseFactory _db;

    public async Task<Server?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Server>(
            "SELECT * FROM Servers WHERE Id = @Id AND IsActive = 1",
            new { Id = id });
    }

    public async Task<int> InsertAsync(Server server)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>("""
            INSERT INTO Servers (Name, Hostname, IpAddress, Environment, AgentApiKey, IsActive, CreatedAt, CreatedBy)
            VALUES (@Name, @Hostname, @IpAddress, @Environment, @AgentApiKey, 1, GETUTCDATE(), @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, server);
    }
}
```

### Unit of Work for transactions
```csharp
public class UnitOfWork : IUnitOfWork
{
    private IDbConnection? _conn;
    private IDbTransaction? _tx;

    public IDbTransaction Begin()
    {
        _conn = _dbFactory.CreateConnection();
        _tx = _conn.BeginTransaction();
        return _tx;
    }

    public void Commit() => _tx?.Commit();
    public void Rollback() => _tx?.Rollback();
    public void Dispose() { _tx?.Dispose(); _conn?.Dispose(); }
}
```

---

## Worker Service Patterns

### Reconnecting SignalR client
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await _agentClient.ConnectAsync(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent disconnected, retrying in 15s");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
```

---

## IIS Management Patterns

### ServerManager usage (requires admin)
```csharp
using var manager = new ServerManager();

// Get site
var site = manager.Sites[siteName];
site.Stop();
manager.CommitChanges();

// Create app pool
var pool = manager.ApplicationPools.Add(poolName);
pool.ManagedRuntimeVersion = "v4.0";
pool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
pool.AutoStart = true;
manager.CommitChanges();
```

### Safe site operations with status check
```csharp
public async Task<Result<bool>> StopSiteAsync(string siteName)
{
    try
    {
        using var mgr = new ServerManager();
        var site = mgr.Sites[siteName];
        if (site == null) return Result<bool>.Fail($"Site '{siteName}' not found");
        if (site.State == ObjectState.Stopped) return Result<bool>.Ok(false);
        site.Stop();
        mgr.CommitChanges();
        return Result<bool>.Ok(true);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to stop site {SiteName}", siteName);
        return Result<bool>.Fail(ex.Message);
    }
}
```

---

## Deployment Automation Patterns

### Deployment lock (file-based per site)
```csharp
public async Task<bool> TryAcquireLockAsync(string siteName)
{
    var lockFile = Path.Combine(_lockDir, $"{siteName}.lock");
    try
    {
        await File.WriteAllTextAsync(lockFile, DateTime.UtcNow.ToString("O"));
        return true;
    }
    catch { return false; }
}
```

### Atomic file replacement
```csharp
// Extract to temp, then move
var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
ZipFile.ExtractToDirectory(packagePath, tempPath);
// Copy files (not Directory.Move — cross-drive safe)
foreach (var file in Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories))
{
    var relative = Path.GetRelativePath(tempPath, file);
    var dest = Path.Combine(physicalPath, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    File.Copy(file, dest, overwrite: true);
}
Directory.Delete(tempPath, recursive: true);
```

---

## Logging Patterns

### Serilog setup (WebPortal)
```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File(new JsonFormatter(), "logs/portal-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

### Serilog appsettings.json
```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

### Structured log with context
```csharp
using (_logger.BeginScope(new { DeploymentId = id, ServerId = serverId }))
{
    _logger.LogInformation("Deployment {DeploymentId} started on server {ServerId}", id, serverId);
}
```

---

## Error Handling Patterns

### Global exception middleware
```csharp
app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = "Internal server error", correlationId = ctx.TraceIdentifier });
}));
```

### FluentValidation
```csharp
public class CreateDeploymentValidator : AbstractValidator<CreateDeploymentDto>
{
    public CreateDeploymentValidator()
    {
        RuleFor(x => x.ApplicationId).GreaterThan(0);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TargetServerIds).NotEmpty().WithMessage("Select at least one server");
    }
}
```

---

## Security Implementation Practices

### Windows Authentication setup
```csharp
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();
builder.Services.AddAuthorization(opts =>
{
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    opts.AddPolicy("AdminOnly", p => p.RequireRole("IISManager-Admins"));
    opts.AddPolicy("OperatorOrAbove", p => p.RequireRole("IISManager-Admins", "IISManager-Operators"));
});
```

### Agent API key validation (Hub filter)
```csharp
public class AgentAuthFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext ctx, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!ctx.Context.Items.ContainsKey("ServerId"))
            throw new HubException("Unauthorized");
        return await next(ctx);
    }
}
```

### Package hash validation
```csharp
public bool ValidatePackageHash(string filePath, string expectedHash)
{
    using var sha = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = Convert.ToHexString(sha.ComputeHash(stream));
    return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);
}
```
