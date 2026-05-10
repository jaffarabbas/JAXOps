using System.Text.Json.Serialization;

namespace IISManager.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventType
{
    Heartbeat,
    DeploymentProgress,
    DeploymentLogLine,
    DeploymentCompleted,
    DeploymentFailed,
    IISStateSnapshot,
    ServerHealthSnapshot,
    CommandAcknowledged,
    CommandFailed
}
