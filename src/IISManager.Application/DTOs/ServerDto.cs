using IISManager.Domain.Enums;

namespace IISManager.Application.DTOs;

public class ServerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string? Description { get; set; }
    public ServerStatus Status { get; set; }
    public string StatusLabel => Status.ToString();
    public DateTime? LastHeartbeat { get; set; }
    public string? AgentVersion { get; set; }
    public bool IsOnline => Status == ServerStatus.Online;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateServerDto
{
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string? Description { get; set; }
}

public class UpdateServerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string? Description { get; set; }
}
