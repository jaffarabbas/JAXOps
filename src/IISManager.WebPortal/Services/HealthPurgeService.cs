using IISManager.Domain.Interfaces;
using IISManager.Shared.Constants;

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
        // Run stale-agent detection every 60 s; piggyback daily purge on the same loop.
        var staleTick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                // Mark any server Online whose heartbeat is older than 2× the heartbeat interval as Offline.
                var serverRepo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
                var staleCutoff = DateTime.UtcNow.AddSeconds(-(SignalRConstants.ConnectionTimeoutSeconds));
                await serverRepo.MarkStaleServersOfflineAsync(staleCutoff);

                // Purge old health snapshots once every 6 hours (360 ticks of 60 s).
                staleTick++;
                if (staleTick >= 360)
                {
                    staleTick = 0;
                    var healthRepo = scope.ServiceProvider.GetRequiredService<IServerHealthRepository>();
                    var purgeCutoff = DateTime.UtcNow.AddDays(-_config.HealthSnapshotRetentionDays);
                    await healthRepo.PurgeOlderThanAsync(purgeCutoff);
                    _logger.LogInformation("Purged server health records older than {Cutoff}", purgeCutoff);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health purge service error");
            }
        }
    }
}
