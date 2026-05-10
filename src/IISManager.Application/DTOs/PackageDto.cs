namespace IISManager.Application.DTOs;

public class PackageDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / 1024.0 / 1024.0:F1} MB"
    };
    public int ApplicationId { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
