using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Hosting;

namespace ClubGear.Services.Core;

public sealed class ProfileImageStorageService : IProfileImageStorageService
{
    private const string PublicPrefix = "/uploads/profile-images/";
    private readonly IWebHostEnvironment _environment;

    public ProfileImageStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveProfileImageAsync(
        int memberId,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var targetDirectory = Path.Combine(webRoot, "uploads", "profile-images");
        Directory.CreateDirectory(targetDirectory);

        var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension.ToLowerInvariant()
            : "." + extension.ToLowerInvariant();

        var fileName = $"member-{memberId}-{Guid.NewGuid():N}{normalizedExtension}";
        var filePath = Path.Combine(targetDirectory, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);

        return PublicPrefix + fileName;
    }

    public Task DeleteProfileImageAsync(string? imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath)
            || !imagePath.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var fileName = Path.GetFileName(imagePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Task.CompletedTask;
        }

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var filePath = Path.Combine(webRoot, "uploads", "profile-images", fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}