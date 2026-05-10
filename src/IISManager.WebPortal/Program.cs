using IISManager.Application.Extensions;
using IISManager.Application.Interfaces;
using IISManager.Domain.Interfaces;
using IISManager.Infrastructure.Extensions;
using IISManager.Infrastructure.Services;
using IISManager.Shared.Constants;
using IISManager.Shared.Extensions;
using IISManager.WebPortal.Hubs;
using IISManager.WebPortal.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// Authentication — Windows Auth
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

// Authorization policies
builder.Services.AddAuthorization(opts =>
{
    opts.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    opts.AddPolicy("AdminOnly", p => p.RequireRole("IISManager-Admins"));
    opts.AddPolicy("OperatorOrAbove", p => p.RequireRole("IISManager-Admins", "IISManager-Operators"));
    opts.AddPolicy("ViewerOrAbove", p => p.RequireRole("IISManager-Admins", "IISManager-Operators", "IISManager-Viewers"));
});

// Razor Pages
// Note: role-based folder policies (OperatorOrAbove, ViewerOrAbove) require AD groups
// IISManager-Admins / IISManager-Operators / IISManager-Viewers to be created in production.
// In development any authenticated Windows user can access all pages.
var isDev = builder.Environment.IsDevelopment();
builder.Services.AddRazorPages(opts =>
{
    opts.Conventions.AuthorizeFolder("/");
    if (!isDev)
    {
        opts.Conventions.AuthorizeFolder("/Servers", "OperatorOrAbove");
        opts.Conventions.AuthorizeFolder("/Deployments", "OperatorOrAbove");
        opts.Conventions.AuthorizeFolder("/Audit", "ViewerOrAbove");
    }
});

// SignalR
builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opts.ClientTimeoutInterval = TimeSpan.FromSeconds(90);
    opts.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Application + Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// Portal-specific services
builder.Services.AddSingleton<AgentHubContextAdapter>();
builder.Services.AddScoped<IAgentHubContext>(sp => sp.GetRequiredService<AgentHubContextAdapter>());
builder.Services.AddScoped<IPackageAppService, PackageAppService>();
builder.Services.AddScoped<IDeploymentBroadcaster, DeploymentBroadcaster>();
builder.Services.AddSingleton<PortalConfiguration>(
    builder.Configuration.GetSection("Portal").Get<PortalConfiguration>() ?? new());

// Background services
builder.Services.AddHostedService<HealthPurgeService>();

// Health checks (basic — add AspNetCore.HealthChecks.SqlServer package for DB check)
builder.Services.AddHealthChecks();

// Anti-forgery
builder.Services.AddAntiforgery();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<AgentHub>("/hubs/agent").AllowAnonymous(); // Agent auth handled inside hub
app.MapHub<DeploymentHub>("/hubs/deployment");
app.MapHub<MonitoringHub>("/hubs/monitoring");
app.MapHealthChecks("/health");

// Package download endpoint — accepts Windows Auth (browser) or Agent API key (agent)
app.MapGet("/api/packages/{id:int}/download", async (int id, HttpContext ctx, IPackageAppService svc, IServerRepository serverRepo) =>
{
    // Allow authenticated browser users through normally
    if (!ctx.User.Identity?.IsAuthenticated ?? true)
    {
        // Check agent API key header
        var apiKey = ctx.Request.Headers[SignalRConstants.AgentApiKeyHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
            return Results.Unauthorized();

        var keyHash = apiKey.ToSha256Hash();
        var server = await serverRepo.GetByApiKeyHashAsync(keyHash);
        if (server is null)
            return Results.Unauthorized();
    }

    var path = await svc.GetDownloadPathAsync(id);
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/zip", Path.GetFileName(path));
}).AllowAnonymous();

// Deployment status polling endpoint (used by Console page)
app.MapGet("/api/deployments/{id:int}/status", async (int id, IDeploymentAppService svc) =>
{
    var d = await svc.GetByIdAsync(id);
    if (d is null) return Results.NotFound();
    return Results.Json(new { status = d.Status.ToString(), failureReason = d.FailureReason });
}).RequireAuthorization();

app.Run();
