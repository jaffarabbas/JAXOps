using IISManager.Application.DTOs;
using IISManager.Application.Interfaces;
using IISManager.Domain.Common;
using IISManager.Domain.Entities;
using IISManager.Domain.Interfaces;
using System.Security.Cryptography;

namespace IISManager.WebPortal.Services;

public class PackageAppService : IPackageAppService
{
    private readonly IPackageRepository _packages;
    private readonly PortalConfiguration _config;
    private readonly ILogger<PackageAppService> _logger;

    public PackageAppService(IPackageRepository packages, PortalConfiguration config, ILogger<PackageAppService> logger)
    {
        _packages = packages;
        _config = config;
        _logger = logger;
    }

    public async Task<IEnumerable<PackageDto>> GetByApplicationAsync(int applicationId)
    {
        var packages = await _packages.GetByApplicationIdAsync(applicationId);
        return packages.Select(MapToDto);
    }

    public async Task<PackageDto?> GetByIdAsync(int id)
    {
        var pkg = await _packages.GetByIdAsync(id);
        return pkg is null ? null : MapToDto(pkg);
    }

    public async Task<Result<PackageDto>> UploadAsync(int applicationId, string version, Stream packageStream, string fileName, string uploadedBy)
    {
        Directory.CreateDirectory(_config.PackageStorePath);
        var storedName = $"{applicationId}_{version}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.zip";
        var storedPath = Path.Combine(_config.PackageStorePath, storedName);

        string hash;
        long size;
        try
        {
            using var sha = SHA256.Create();
            using var fileStream = File.Create(storedPath);
            using var cryptoStream = new CryptoStream(fileStream, sha, CryptoStreamMode.Write);
            await packageStream.CopyToAsync(cryptoStream);
            await cryptoStream.FlushFinalBlockAsync();
            hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            size = new FileInfo(storedPath).Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded package");
            return Result<PackageDto>.Fail("Failed to store package: " + ex.Message);
        }

        var package = new DeploymentPackage
        {
            FileName = fileName,
            StoredPath = storedPath,
            Sha256Hash = hash,
            SizeBytes = size,
            Version = version,
            ApplicationId = applicationId,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
        };

        var id = await _packages.InsertAsync(package);
        package.Id = id;
        return Result<PackageDto>.Ok(MapToDto(package));
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var pkg = await _packages.GetByIdAsync(id);
        if (pkg is null) return Result.Fail("Package not found");
        await _packages.MarkDeletedAsync(id);
        try { if (File.Exists(pkg.StoredPath)) File.Delete(pkg.StoredPath); } catch { }
        return Result.Ok();
    }

    public async Task<string> GetDownloadPathAsync(int id)
    {
        var pkg = await _packages.GetByIdAsync(id);
        return pkg?.StoredPath ?? string.Empty;
    }

    private static PackageDto MapToDto(DeploymentPackage p) => new()
    {
        Id = p.Id,
        FileName = p.FileName,
        Version = p.Version,
        Sha256Hash = p.Sha256Hash,
        SizeBytes = p.SizeBytes,
        ApplicationId = p.ApplicationId,
        UploadedAt = p.UploadedAt,
        UploadedBy = p.UploadedBy
    };
}
