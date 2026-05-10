using IISManager.AgentService.CommandHandlers;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Events;
using IISManager.Shared.Constants;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace IISManager.AgentService.Services;

public class AgentSignalRClient
{
    private readonly AgentConfiguration _config;
    private readonly IServiceProvider _sp;
    private readonly HealthReportingService _health;
    private readonly ILogger<AgentSignalRClient> _logger;
    private HubConnection? _connection;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgentSignalRClient(
        AgentConfiguration config,
        IServiceProvider sp,
        HealthReportingService health,
        ILogger<AgentSignalRClient> logger)
    {
        _config = config;
        _sp = sp;
        _health = health;
        _logger = logger;
    }

    public async Task ConnectAndRunAsync(CancellationToken ct)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(_config.PortalUrl + SignalRConstants.Hubs.Agent, opts =>
            {
                opts.Headers.Add(SignalRConstants.AgentApiKeyHeader, _config.ApiKey);
                opts.Headers.Add(SignalRConstants.ServerIdHeader, _config.ServerId.ToString());
                if (_config.AllowInsecureSsl)
                    opts.HttpMessageHandlerFactory = h =>
                    {
                        if (h is HttpClientHandler hch)
                            hch.ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        return h;
                    };
            })
            .WithAutomaticReconnect(new[] {
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        RegisterHandlers(_connection);

        _connection.Reconnected += async connectionId =>
        {
            _logger.LogInformation("Agent reconnected with ConnectionId {ConnectionId}", connectionId);
            await SendHeartbeatAsync(ct);
        };

        _connection.Closed += async ex =>
        {
            if (ex is not null)
                _logger.LogWarning(ex, "Agent connection closed");
        };

        await _connection.StartAsync(ct);
        _logger.LogInformation("Agent connected to portal at {Url}", _config.PortalUrl);

        // Initial heartbeat
        await SendHeartbeatAsync(ct);

        // Periodic heartbeat loop
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(SignalRConstants.HeartbeatIntervalSeconds));
        while (await heartbeatTimer.WaitForNextTickAsync(ct))
        {
            await SendHeartbeatAsync(ct);
        }
    }

    private void RegisterHandlers(HubConnection conn)
    {
        conn.On<JsonElement>(SignalRConstants.AgentMethods.ExecuteCommand, async payload =>
        {
            try
            {
                var command = DeserializeCommand(payload);
                if (command is null)
                {
                    _logger.LogWarning("Received unknown command payload");
                    return;
                }
                var dispatcher = _sp.GetRequiredService<CommandDispatcher>();
                await dispatcher.DispatchAsync(command, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command");
            }
        });

        conn.On(SignalRConstants.AgentMethods.Ping, async () =>
        {
            await SendHeartbeatAsync(CancellationToken.None);
        });
    }

    public async Task SendEventAsync<T>(string method, T eventData, CancellationToken ct = default)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync(method, eventData, ct);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        var heartbeat = await _health.BuildHeartbeatAsync();
        await SendEventAsync(SignalRConstants.PortalMethods.ReportHeartbeat, heartbeat, ct);
    }

    private AgentCommandBase? DeserializeCommand(JsonElement payload)
    {
        // Peek at CommandType to deserialize into the correct concrete type
        if (!payload.TryGetProperty("commandType", out var typeEl))
            return null;

        var commandTypeName = typeEl.GetString();
        return commandTypeName switch
        {
            "DeployApplication" => JsonSerializer.Deserialize<DeployApplicationCommand>(payload.GetRawText(), JsonOptions),
            "RollbackDeployment" => JsonSerializer.Deserialize<RollbackCommand>(payload.GetRawText(), JsonOptions),
            "StartWebsite" or "StopWebsite" or "RestartWebsite" or "CreateWebsite" or "DeleteWebsite"
                or "SyncIISState" => JsonSerializer.Deserialize<WebsiteCommand>(payload.GetRawText(), JsonOptions),
            "RestartApplicationPool" or "StopApplicationPool" or "StartApplicationPool"
                or "CreateApplicationPool" or "DeleteApplicationPool" or "RecycleApplicationPool"
                or "GetIISStatus" or "GetServerHealth"
                => JsonSerializer.Deserialize<AppPoolCommand>(payload.GetRawText(), JsonOptions),
            _ => null
        };
    }
}
