namespace IISManager.WebPortal.Services;

public class PortalConfiguration
{
    public string PackageStorePath { get; set; } = "C:\\IISManagerPackages";
    public int MaxPackageSizeMB { get; set; } = 500;
    public int HealthSnapshotRetentionDays { get; set; } = 30;
}
