using Microsoft.AspNetCore.Http;

namespace HospitalManagement.Infrastructure;

/// <summary>Stores validated profile images outside wwwroot; downloads are authorized by the account controller.</summary>
public sealed class LocalProfileImageStorage(IWebHostEnvironment environment) : IProfileImageStorage
{
    public const long MaximumImageBytes = 5 * 1024 * 1024;
    private readonly string _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "profile-images"));

    public async Task<StoredProfileImage?> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length is <= 0 or > MaximumImageBytes)
        {
            return null;
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return null;
        }

        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is <= 0 or > MaximumImageBytes || !HasExpectedSignature(buffer.ToArray(), extension))
        {
            return null;
        }

        Directory.CreateDirectory(_root);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The generated profile image path was invalid.");
        }

        await File.WriteAllBytesAsync(fullPath, buffer.ToArray(), cancellationToken);
        return new StoredProfileImage(fileName, fullPath, ContentTypeFor(extension));
    }

    public bool TryResolve(string? fileName, out StoredProfileImage? image)
    {
        image = null;
        if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(fullPath))
        {
            return false;
        }

        image = new StoredProfileImage(fileName, fullPath, ContentTypeFor(extension));
        return true;
    }

    private static bool HasExpectedSignature(byte[] data, string extension) => extension switch
    {
        ".jpg" or ".jpeg" => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
        ".png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ".webp" => data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private static string ContentTypeFor(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
