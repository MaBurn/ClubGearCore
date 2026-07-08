using System.Security.Cryptography;
using System.Text;
using ClubGear.Services.Plugins.Security;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginIntegrityVerifierTests
{
    private readonly PluginIntegrityVerifier _sut = new();

    [Fact]
    public void Verify_ReturnsValid_ForSignedUntamperedPackage()
    {
        var packageBytes = Encoding.UTF8.GetBytes("plugin-bundle-v1");
        var expectedHash = SHA256.HashData(packageBytes);

        using var rsa = RSA.Create(2048);
        var signature = rsa.SignHash(expectedHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var request = new PluginIntegrityVerificationRequest(packageBytes, expectedHash, signature, publicKeyPem);

        var result = _sut.Verify(request);

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
        Assert.Equal(Convert.ToHexString(expectedHash), result.ComputedHashHex);
    }

    [Fact]
    public void Verify_RejectsTamperedPackage_WhenHashAndSignatureNoLongerMatch()
    {
        var originalBytes = Encoding.UTF8.GetBytes("plugin-bundle-v1");
        var expectedHash = SHA256.HashData(originalBytes);

        using var rsa = RSA.Create(2048);
        var signature = rsa.SignHash(expectedHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var tamperedBytes = Encoding.UTF8.GetBytes("plugin-bundle-v1-tampered");
        var request = new PluginIntegrityVerificationRequest(tamperedBytes, expectedHash, signature, publicKeyPem);

        var result = _sut.Verify(request);

        Assert.False(result.IsValid);
        Assert.Contains("hash mismatch", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}