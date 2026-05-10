using IISManager.AgentService.Services;
using IISManager.Contracts.Commands;
using IISManager.Contracts.Enums;

namespace IISManager.AgentService.CommandHandlers;

public class DeployCommandHandler : ICommandHandler
{
    private readonly DeploymentExecutorService _executor;

    public DeployCommandHandler(DeploymentExecutorService executor) => _executor = executor;

    public bool CanHandle(AgentCommandBase command) =>
        command.CommandType == CommandType.DeployApplication;

    public async Task HandleAsync(AgentCommandBase command, CancellationToken ct)
    {
        var deployCmd = (DeployApplicationCommand)command;
        await _executor.ExecuteAsync(deployCmd, ct);
    }
}
