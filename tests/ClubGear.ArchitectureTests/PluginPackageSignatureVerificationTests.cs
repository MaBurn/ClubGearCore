using ClubGear.Services.Plugins.Security;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 7 (Finance Nav/Self-Service/IBAN bugfix) verification: after repackaging
/// Finance, CarInfo, and ServiceBook against the updated contracts DLL, each
/// plugin's freshly produced distributable (zip + sha256 + signature + public key)
/// must still pass <see cref="PluginIntegrityVerifier"/> — i.e. the host's plugin
/// loader accepts all three packages without signature validation errors.
/// </summary>
public sealed class PluginPackageSignatureVerificationTests
{
    private readonly PluginIntegrityVerifier _sut = new();

    [Theory]
    [InlineData("Finance", "Finance-1.6.0")]
    [InlineData("CarInfo", "CarInfo-1.0.6")]
    [InlineData("ServiceBook", "ServiceBook-1.0.3")]
    public void Verify_AcceptsFreshlyPackagedPlugin_WithoutSignatureValidationErrors(string pluginDir, string packageName)
    {
        var distDir = GetProjectFilePath("plugins", pluginDir, "dist");
        var zipPath = Path.Combine(distDir, $"{packageName}.zip");
        var sha256Path = Path.Combine(distDir, $"{packageName}.sha256");
        var signaturePath = Path.Combine(distDir, $"{packageName}.signature.bin");
        var publicKeyPath = Path.Combine(distDir, "signer-public.pem");

        Assert.True(File.Exists(zipPath), $"Package not found: {zipPath}");
        Assert.True(File.Exists(sha256Path), $"Hash file not found: {sha256Path}");
        Assert.True(File.Exists(signaturePath), $"Signature file not found: {signaturePath}");
        Assert.True(File.Exists(publicKeyPath), $"Public key not found: {publicKeyPath}");

        var packageBytes = File.ReadAllBytes(zipPath);
        var expectedSha256 = Convert.FromHexString(File.ReadAllText(sha256Path).Trim());
        var signature = File.ReadAllBytes(signaturePath);
        var publicKeyPem = File.ReadAllText(publicKeyPath);

        var request = new PluginIntegrityVerificationRequest(packageBytes, expectedSha256, signature, publicKeyPem);

        var result = _sut.Verify(request);

        Assert.True(result.IsValid, $"Signature validation failed for {packageName}: {result.Reason}");
        Assert.Null(result.Reason);
    }

    private static string GetProjectFilePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var csprojPath = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(csprojPath))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }
}
