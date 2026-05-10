using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Events;

public class CommandResultEvent : AgentEventBase
{
    public Guid CommandId { get; set; }
    public CommandType CommandType { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
}
