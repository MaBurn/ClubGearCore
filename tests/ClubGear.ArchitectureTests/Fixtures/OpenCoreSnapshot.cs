namespace ClubGear.ArchitectureTests.Fixtures;

/// <summary>
/// The public GitHub mirror (<c>scripts/publish-release-github.sh</c>) ships the core without
/// <c>plugins/</c> and without <c>.gitmodules</c>. Tests that inspect the plugin submodules or their
/// built packages return early there, the same way <c>MemberIndexGroupFilterTests</c> does on a
/// machine without Node.
///
/// Both markers must be absent. A private clone that only forgot
/// <c>git submodule update --init</c> still has <c>.gitmodules</c> and an empty <c>plugins/</c>
/// directory, so those tests keep failing loudly there.
/// </summary>
internal static class OpenCoreSnapshot
{
    public static bool IsCurrent(string repositoryRoot) =>
        !File.Exists(Path.Combine(repositoryRoot, ".gitmodules"))
        && !Directory.Exists(Path.Combine(repositoryRoot, "plugins"));
}
