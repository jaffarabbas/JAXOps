using IISManager.Contracts.Commands;

namespace IISManager.AgentService.CommandHandlers;

public class CommandDispatcher
{
    private readonly IEnumerable<ICommandHandler> _handlers;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IEnumerable<ICommandHandler> handlers, ILogger<CommandDispatcher> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task DispatchAsync(AgentCommandBase command, CancellationToken ct)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(command));
        if (handler is null)
        {
            _logger.LogWarning("No handler found for command {CommandType}", command.CommandType);
            return;
        }

        _logger.LogInformation("Dispatching command {CommandType} (Id={CommandId})", command.CommandType, command.CommandId);

        try
        {
            await handler.HandleAsync(command, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandType} handler threw an exception", command.CommandType);
        }
    }
}
