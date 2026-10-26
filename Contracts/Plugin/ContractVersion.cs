namespace ClubGear.Plugin.Contracts;

public static class ContractVersion
{
    public static readonly Version Current = new(1, 15, 0);

    // 1.15.0 only adds an init-only IsIconColumn to PluginPageColumn, so a plugin page's list and
    // detail views can render one column as a Bootstrap Icon instead of text. The existing
    // positional constructor is unchanged, so the compatibility floor stays put.
    // 1.14.0 only adds init-only grouping metadata to PluginNavEntry (GroupKey/GroupLabel/
    // GroupIcon) and an init-only PageKey to PluginPageProviderContribution, so a module can
    // register more than one page.generic provider. Both types keep their existing positional
    // constructors, so the compatibility floor stays put.
    // 1.13.0 REMOVES a shape: the parameterless construction contract. Contributions are no
    // longer created with Activator.CreateInstance(type); the host constructs them through a
    // scope using the single canonical public constructor T(IPluginHostContext). Because a shape
    // was removed and not merely added, MinimumSupported moves with Current — an installed plugin
    // built against 1.12.0 or lower is refused at load time with a version message rather than
    // failing later with a construction error. The module entry point (IPluginModule) is the one
    // documented exception and keeps its parameterless constructor; see docs/plugin-contract.md.
    // 1.12.0 only added members: PluginCapabilityKeys and PluginCapabilityArity are new types,
    // and no existing type changed shape. A plugin built against 1.11.0 keeps working without
    // recompiling — unless it already declared a service contribution, which until now was
    // collected and never read. Those keys are now validated at load time, so a plugin that
    // claimed an unrecognised capability is rejected instead of silently doing nothing.
    // 1.11.0 only added members: a default-implemented interface method and two init-only
    // properties whose records kept their existing constructors. Nothing was removed, so the
    // floor stays where it is.
    public static readonly Version MinimumSupported = new(1, 13, 0);
}
