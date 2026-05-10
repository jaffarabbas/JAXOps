using IISManager.AgentService;
using IISManager.AgentService.CommandHandlers;
using IISManager.AgentService.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service support
builder.Services.AddWindowsService(opts =>
    opts.ServiceName = "IISManagerAgent");

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Services.AddSerilog();

// Agent configuration
var agentConfig = builder.Configuration.GetSection("Agent").Get<AgentConfiguration>()
    ?? throw new InvalidOperationException("Agent configuration section is required");
builder.Services.AddSingleton(agentConfig);

// Core services
builder.Services.AddSingleton<IISManagementService>();
builder.Services.AddSingleton<DeploymentExecutorService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<HealthReportingService>();

// Command handlers
builder.Services.AddSingleton<ICommandHandler, DeployCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, IISCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RollbackCommandHandler>();
builder.Services.AddSingleton<CommandDispatcher>();

// SignalR client (agent → portal)
builder.Services.AddSingleton<AgentSignalRClient>();

// Main worker
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
await host.RunAsync();
