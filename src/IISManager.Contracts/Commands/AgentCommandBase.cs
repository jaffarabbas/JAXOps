using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Commands;

public abstract class AgentCommandBase
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public CommandType CommandType { get; set; }
    public int ServerId { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public string IssuedBy { get; set; } = string.Empty;
}
