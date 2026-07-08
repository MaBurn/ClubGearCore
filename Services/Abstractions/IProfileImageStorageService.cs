namespace ClubGear.Services.Abstractions;

public interface IProfileImageStorageService
{
    Task<string> SaveProfileImageAsync(
        int memberId,
        string extension,
        Stream content,
        CancellationToken cancellationToken = default);

    Task DeleteProfileImageAsync(string? imagePath, CancellationToken cancellationToken = default);
}