using IISManager.Contracts.Commands;
using IISManager.Domain.Common;

namespace IISManager.Application.Interfaces;

public interface IAgentCommunicationService
{
    Task<Result> SendCommandAsync(int serverId, AgentCommandBase command, CancellationToken ct = default);
    Task<bool> IsServerOnlineAsync(int serverId);
}
