using IISManager.Application.DTOs;
using IISManager.Domain.Common;

namespace IISManager.Application.Interfaces;

public interface IPackageAppService
{
    Task<IEnumerable<PackageDto>> GetByApplicationAsync(int applicationId);
    Task<PackageDto?> GetByIdAsync(int id);
    Task<Result<PackageDto>> UploadAsync(int applicationId, string version, Stream packageStream, string fileName, string uploadedBy);
    Task<Result> DeleteAsync(int id);
    Task<string> GetDownloadPathAsync(int id);
}
