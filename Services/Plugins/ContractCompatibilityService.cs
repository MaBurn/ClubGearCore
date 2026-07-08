using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins;

public sealed class ContractCompatibilityService : IContractCompatibilityService
{
    public ContractCompatibilityResult Validate(Version pluginContractVersion)
    {
        if (pluginContractVersion.Major != ContractVersion.Current.Major)
        {
            return new ContractCompatibilityResult(
                false,
                $"Incompatible major version. Expected {ContractVersion.Current.Major}, got {pluginContractVersion.Major}.");
        }

        if (pluginContractVersion < ContractVersion.MinimumSupported)
        {
            return new ContractCompatibilityResult(
                false,
                $"Contract version {pluginContractVersion} is below minimum supported version {ContractVersion.MinimumSupported}.");
        }

        return new ContractCompatibilityResult(true);
    }
}