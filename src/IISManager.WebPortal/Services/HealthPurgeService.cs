using IISManager.Domain.Interfaces;

namespace IISManager.WebPortal.Services;

public class HealthPurgeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PortalConfiguration _config;
    private readonly ILogger<HealthPurgeService> _logger;

    public HealthPurgeService(IServiceScopeFactory scopeFactory, PortalConfiguration config, ILogger<HealthPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IServerHealthRepository>();
                var cutoff = DateTime.UtcNow.AddDays(-_config.HealthSnapshotRetentionDays);
                await repo.PurgeOlderThanAsync(cutoff);
                _logger.LogInformation("Purged server health records older than {Cutoff}", cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health purge service error");
            }
        }
    }
}
