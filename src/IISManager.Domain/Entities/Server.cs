using IISManager.Domain.Enums;

namespace IISManager.Domain.Entities;

public class Server
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string? Description { get; set; }
    public string AgentApiKey { get; set; } = string.Empty;
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;
    public string? AgentConnectionId { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public string? AgentVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
