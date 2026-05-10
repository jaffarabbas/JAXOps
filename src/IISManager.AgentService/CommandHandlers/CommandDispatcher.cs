using IISManager.AgentService.Services;
using IISManager.Contracts.Commands;

namespace IISManager.AgentService.CommandHandlers;

public class CommandDispatcher
{
    private readonly IEnumerable<ICommandHandler> _handlers;
    private readonly AgentSignalRClient _client;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        IEnumerable<ICommandHandler> handlers,
        AgentSignalRClient client,
        ILogger<CommandDispatcher> logger)
    {
        _handlers = handlers;
        _client = client;
        _logger = logger;
    }

    public async Task DispatchAsync(AgentCommandBase command, CancellationToken ct)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(command));
        if (handler is null)
        {
            _logger.LogWarning("No handler found for command {CommandType}", command.CommandType);
            await _client.SendAgentLogAsync($"Unhandled command: {command.CommandType}", "Warning", "Dispatcher", ct);
            return;
        }

        _logger.LogInformation("Dispatching command {CommandType} (Id={CommandId})", command.CommandType, command.CommandId);
        await _client.SendAgentLogAsync($"→ {command.CommandType} received (id={command.CommandId})", "Info", "Dispatcher", ct);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await handler.HandleAsync(command, ct);
            sw.Stop();
            await _client.SendAgentLogAsync($"✓ {command.CommandType} completed in {sw.ElapsedMilliseconds} ms", "Info", "Dispatcher", ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Command {CommandType} handler threw an exception", command.CommandType);
            await _client.SendAgentLogAsync($"✗ {command.CommandType} failed: {ex.Message}", "Error", "Dispatcher", ct);
        }
    }
}
