namespace ClubGear.Services.Abstractions;

public interface IContractCompatibilityService
{
    ContractCompatibilityResult Validate(Version pluginContractVersion);
}

public sealed record ContractCompatibilityResult(bool IsCompatible, string? Reason = null);