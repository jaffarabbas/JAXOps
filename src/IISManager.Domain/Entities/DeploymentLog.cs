namespace IISManager.Domain.Entities;

public class DeploymentLog
{
    public long Id { get; set; }
    public int DeploymentId { get; set; }
    public int? ServerId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
}
