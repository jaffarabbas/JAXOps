namespace IISManager.Domain.Entities;

public class Application
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultServerId { get; set; }
    public string DefaultWebsiteName { get; set; } = string.Empty;
    public string DefaultAppPoolName { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
