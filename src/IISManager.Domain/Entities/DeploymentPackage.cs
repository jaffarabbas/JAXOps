namespace IISManager.Domain.Entities;

public class DeploymentPackage
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Version { get; set; } = string.Empty;
    public int ApplicationId { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
