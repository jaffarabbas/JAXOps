namespace IISManager.Domain.Entities;

public class Notification
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public string? ActionUrl { get; set; }
    public string? TargetUser { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
