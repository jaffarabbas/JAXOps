using IISManager.Contracts.Enums;

namespace IISManager.Contracts.Events;

public abstract class AgentEventBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public EventType EventType { get; set; }
    public int ServerId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
