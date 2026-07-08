namespace ClubGear.Services.Plugins.Security;

public interface IPluginIntegrityVerifier
{
    PluginIntegrityVerificationResult Verify(PluginIntegrityVerificationRequest request);
}

public sealed record PluginIntegrityVerificationRequest(
    byte[] PackageBytes,
    byte[] ExpectedSha256,
    byte[] Signature,
    string SignerPublicKeyPem);

public sealed record PluginIntegrityVerificationResult(
    bool IsValid,
    string ComputedHashHex,
    string? Reason = null);