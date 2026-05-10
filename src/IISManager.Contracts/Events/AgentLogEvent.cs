namespace IISManager.Contracts.Events;

public class AgentLogEvent : AgentEventBase
{
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public string? Category { get; set; }
}
