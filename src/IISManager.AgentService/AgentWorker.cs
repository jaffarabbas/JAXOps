using IISManager.AgentService.Services;

namespace IISManager.AgentService;

public class AgentWorker : BackgroundService
{
    private readonly AgentSignalRClient _client;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(AgentSignalRClient client, ILogger<AgentWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IIS Manager Agent starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.ConnectAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent disconnected unexpectedly, retrying in 15s");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        _logger.LogInformation("IIS Manager Agent stopped");
    }
}
