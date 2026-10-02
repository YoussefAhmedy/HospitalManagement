using Microsoft.AspNetCore.Http;

namespace HospitalManagement.Infrastructure;

public sealed record StoredProfileImage(string FileName, string PhysicalPath, string ContentType);

public interface IProfileImageStorage
{
    Task<StoredProfileImage?> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    bool TryResolve(string? fileName, out StoredProfileImage? image);
}
