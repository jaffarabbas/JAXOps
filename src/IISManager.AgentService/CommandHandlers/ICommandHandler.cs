using IISManager.Contracts.Commands;

namespace IISManager.AgentService.CommandHandlers;

public interface ICommandHandler
{
    bool CanHandle(AgentCommandBase command);
    Task HandleAsync(AgentCommandBase command, CancellationToken ct);
}
