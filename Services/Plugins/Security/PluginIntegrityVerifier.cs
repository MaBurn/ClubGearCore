using System.Security.Cryptography;

namespace ClubGear.Services.Plugins.Security;

public sealed class PluginIntegrityVerifier : IPluginIntegrityVerifier
{
    public PluginIntegrityVerificationResult Verify(PluginIntegrityVerificationRequest request)
    {
        if (request.PackageBytes.Length == 0)
        {
            return new PluginIntegrityVerificationResult(false, string.Empty, "Package payload is empty.");
        }

        if (request.ExpectedSha256.Length == 0)
        {
            return new PluginIntegrityVerificationResult(false, string.Empty, "Expected SHA-256 hash is missing.");
        }

        if (request.Signature.Length == 0)
        {
            return new PluginIntegrityVerificationResult(false, string.Empty, "Package signature is missing.");
        }

        var computedHash = SHA256.HashData(request.PackageBytes);
        var computedHashHex = Convert.ToHexString(computedHash);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, request.ExpectedSha256))
        {
            return new PluginIntegrityVerificationResult(false, computedHashHex, "Package hash mismatch. Package may be tampered.");
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(request.SignerPublicKeyPem);
        }
        catch (Exception ex)
        {
            return new PluginIntegrityVerificationResult(false, computedHashHex, $"Signer public key is invalid: {ex.Message}");
        }

        var signatureIsValid = rsa.VerifyHash(
            computedHash,
            request.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (!signatureIsValid)
        {
            return new PluginIntegrityVerificationResult(false, computedHashHex, "Package signature validation failed.");
        }

        return new PluginIntegrityVerificationResult(true, computedHashHex);
    }
}