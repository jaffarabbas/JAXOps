namespace IISManager.Application.Interfaces;

public interface IDeploymentBroadcaster
{
    Task BroadcastLogLineAsync(int deploymentId, int? serverId, string message, string level);
    Task BroadcastStatusChangeAsync(int deploymentId, string status, int? serverId = null);
}
