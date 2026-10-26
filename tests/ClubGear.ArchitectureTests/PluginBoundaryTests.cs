using System.Reflection;
using System.Text;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Runtime;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginBoundaryTests
{
    [Fact]
    public void ContractsAssembly_ShouldNotReference_CoreAssembly()
    {
        var referencedAssemblies = typeof(IPluginModule)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.DoesNotContain("ClubGear", referencedAssemblies, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractsAssembly_ShouldExpose_HostContext_AndMemberReadModels()
    {
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginHostContext).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginMemberSummary).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(PluginMemberDetail).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberDetailCardProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberEditTabProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberStatusBadgeProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IMemberActionProvider).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginMigration).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginDataStore).Assembly);
        Assert.Same(typeof(IPluginModule).Assembly, typeof(IPluginMigrationContext).Assembly);

        var propertyTypes = typeof(IPluginHostContext)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.Contains(typeof(IPluginMetadataFacade), propertyTypes);
        Assert.Contains(typeof(IPluginMemberReader), propertyTypes);
        Assert.Contains(typeof(IPluginMemberActionFacade), propertyTypes);
        Assert.Contains(typeof(IPluginDataStore), propertyTypes);
    }

    [Fact]
    public void ContractsProject_ShouldNotReference_CoreProject()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var contractsProject = Path.Combine(repositoryRoot, "Contracts", "Plugin", "ClubGear.Plugin.Contracts.csproj");
        var projectContent = File.ReadAllText(contractsProject);

        Assert.DoesNotContain("ClubGear.csproj", projectContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContractsSource_ShouldNotUse_ForbiddenCoreNamespaces()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var contractsFolder = Path.Combine(repositoryRoot, "Contracts", "Plugin");
        var sourceFiles = Directory
            .EnumerateFiles(contractsFolder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        var forbiddenNamespaces = new[]
        {
            "ClubGear.Services",
            "ClubGear.Data",
            "ClubGear.Controllers",
            "ClubGear.Models"
        };

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);

            foreach (var forbiddenNamespace in forbiddenNamespaces)
            {
                Assert.DoesNotContain(forbiddenNamespace, content, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// CORE-16. The source guardrail below is only worth anything when the submodules are actually
    /// on disk. Without this check a fresh clone without <c>git submodule update --init --recursive</c>
    /// would scan nothing and report success, which reads as coverage while covering nothing.
    /// Mirrors the <c>submodules</c> validate job in <c>.gitlab-ci.yml</c>.
    /// </summary>
    [Fact]
    public void PluginSubmodules_ShouldBe_CheckedOut()
    {
        var missingModules = FindMissingPluginModules(ResolveRepositoryRoot());

        Assert.True(missingModules.Count == 0, DescribeMissingModules(missingModules));
    }

    [Fact]
    public void MissingPluginSubmodules_ShouldFail_WithCheckoutMessage()
    {
        var emptyRoot = CreateTemporaryRoot();

        try
        {
            var missingModules = FindMissingPluginModules(emptyRoot);

            Assert.Equal(PluginModules, missingModules);
            Assert.Contains("submodules not checked out", DescribeMissingModules(missingModules), StringComparison.Ordinal);
            Assert.Contains("git submodule update --init --recursive", DescribeMissingModules(missingModules), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    /// <summary>
    /// CORE-16. A plugin talks to the host through <c>ClubGear.Plugin.Contracts</c> only, so no
    /// submodule source file may name a core namespace or the core <see cref="ApplicationDbContext"/>.
    ///
    /// This is a source scan on purpose, not a reflection scan: <c>plugins/IamOidc</c> is not a
    /// <c>ProjectReference</c> of this test project, so a reflection-based check would silently skip
    /// the submodule with the most host-adjacent code. The forbidden set is read from
    /// <see cref="PluginIsolationScanner.ForbiddenNamespaces"/> rather than re-declared, so the
    /// load-time gate and this build-time gate cannot drift apart.
    /// </summary>
    [Fact]
    public void PluginSources_ShouldNotReference_ForbiddenCoreNamespaces()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var missingModules = FindMissingPluginModules(repositoryRoot);
        Assert.True(missingModules.Count == 0, DescribeMissingModules(missingModules));

        var scannedFiles = EnumeratePluginSourceFiles(repositoryRoot).ToArray();
        Assert.NotEmpty(scannedFiles);

        foreach (var module in PluginModules)
        {
            var modulePrefix = Path.Combine(repositoryRoot, "plugins", module) + Path.DirectorySeparatorChar;
            Assert.Contains(scannedFiles, file => file.StartsWith(modulePrefix, StringComparison.Ordinal));
        }

        var violations = FindForbiddenReferences(repositoryRoot, scannedFiles);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PluginSourceScan_ShouldReport_PlantedViolation()
    {
        var plantedRoot = CreateTemporaryRoot();

        try
        {
            var moduleFolder = Path.Combine(plantedRoot, "plugins", "CarInfo");
            Directory.CreateDirectory(moduleFolder);
            var plantedFile = Path.Combine(moduleFolder, "Planted.cgcs");
            File.WriteAllText(plantedFile, "using ClubGear.Data;" + Environment.NewLine);

            var ignoredFolder = Path.Combine(moduleFolder, "obj");
            Directory.CreateDirectory(ignoredFolder);
            File.WriteAllText(Path.Combine(ignoredFolder, "Generated.cs"), "using ClubGear.Data;");

            var scannedFiles = EnumeratePluginSourceFiles(plantedRoot).ToArray();
            Assert.Equal(new[] { plantedFile }, scannedFiles);

            var violations = FindForbiddenReferences(plantedRoot, scannedFiles);

            var violation = Assert.Single(violations);
            Assert.Contains(Path.Combine("plugins", "CarInfo", "Planted.cgcs"), violation, StringComparison.Ordinal);
            Assert.Contains("ClubGear.Data", violation, StringComparison.Ordinal);
            Assert.Contains("line 1", violation, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plantedRoot, recursive: true);
        }
    }

    /// <summary>
    /// CORE-23. A plugin submodule must be a gitlink: <c>plugins/&lt;X&gt;/.git</c> is a file pointing into
    /// <c>.git/modules/</c>, never an embedded repository directory. An embedded repository looks checked
    /// out but is invisible to the superproject, so its commits are never recorded and a clone of this
    /// repository silently gets different plugin sources than the machine that pushed.
    /// Mirrors the <c>submodules</c> validate job in <c>.gitlab-ci.yml</c>.
    /// </summary>
    [Fact]
    public void PluginSubmodules_ShouldNotBe_EmbeddedRepositories()
    {
        var repositoryRoot = ResolveRepositoryRoot();

        var violations = FindSubmoduleHygieneViolations(repositoryRoot)
            .Where(violation => violation.Contains(EmbeddedRepositoryMarker, StringComparison.Ordinal))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));

        foreach (var module in PluginModules)
        {
            var gitEntry = Path.Combine(repositoryRoot, "plugins", module, ".git");

            Assert.True(
                File.Exists(gitEntry),
                $"plugins/{module}/.git is missing, expected a gitlink file. "
                + $"Remedy: git submodule update --init plugins/{module}.");
        }
    }

    /// <summary>
    /// CORE-23. The effective remote of every initialised plugin submodule must resolve to the URL
    /// declared in <c>.gitmodules</c> under <see cref="NormalizeRemoteUrl"/>. A local filesystem URL in
    /// <c>.git/config</c> still fetches and still looks healthy, while pushing nothing anybody else can
    /// clone. Normalisation is what keeps the CI HTTPS rewrite from reading as a divergence.
    /// Mirrors the <c>submodules</c> validate job in <c>.gitlab-ci.yml</c>.
    /// </summary>
    [Fact]
    public void PluginSubmoduleUrls_ShouldMatch_GitmodulesDeclaration()
    {
        var repositoryRoot = ResolveRepositoryRoot();

        var declaredUrls = ReadDeclaredSubmoduleUrls(repositoryRoot);
        Assert.NotEmpty(declaredUrls);

        var violations = FindSubmoduleHygieneViolations(repositoryRoot)
            .Where(violation => violation.Contains(DivergentUrlMarker, StringComparison.Ordinal)
                                || violation.Contains(UndeclaredModuleMarker, StringComparison.Ordinal))
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// CORE-23. The normalisation rule has to hold identically in two languages, so it is pinned by a
    /// committed vector file rather than by prose. This theory is the authoritative consumer of
    /// <c>Fixtures/submodule-url-normalization.cases.tsv</c>; the <c>normalize()</c> shell function in the
    /// <c>submodules</c> validate job of <c>.gitlab-ci.yml</c> reads the same rows. Neither implementation
    /// can drift from the rule, or from the other, without a red build.
    /// </summary>
    [Theory]
    [MemberData(nameof(SubmoduleUrlNormalizationVectors))]
    public void SubmoduleUrlNormalization_ShouldMatch_SharedVectors(string rawUrl, string expectedCanonicalUrl)
    {
        Assert.Equal(expectedCanonicalUrl, NormalizeRemoteUrl(rawUrl));
    }

    [Fact]
    public void SubmoduleHygiene_ShouldReport_PlantedViolations()
    {
        // The planted declaration is generated from PluginModules rather than spelled out, so adding a
        // module to the single list above cannot turn this negative-path test red for the wrong reason.
        const string embeddedModule = "CarInfo";
        const string divergentModule = "Finance";
        const string rewrittenModule = "IamOidc";

        Assert.Contains(embeddedModule, PluginModules);
        Assert.Contains(divergentModule, PluginModules);
        Assert.Contains(rewrittenModule, PluginModules);

        var plantedRoot = CreateTemporaryRoot();

        try
        {
            var declared = new StringBuilder();

            foreach (var module in PluginModules)
            {
                declared.Append($"[submodule \"plugins/{module}\"]\n")
                    .Append($"\tpath = plugins/{module}\n")
                    .Append($"\turl = {DeclaredTestUrl(module)}\n");
            }

            File.WriteAllText(Path.Combine(plantedRoot, ".gitmodules"), declared.ToString());

            // embeddedModule: declared and effective agree, but the work tree holds an embedded repository.
            // divergentModule: a proper gitlink whose effective URL points at a local path (CORE-23).
            // rewrittenModule: effective URL is the CI HTTPS rewrite of the declaration - not a violation.
            // Every other module: declared but absent from .git/config, i.e. not initialised, which
            // PluginSubmodules_ShouldBe_CheckedOut owns and this guard deliberately stays out of.
            var effective = new StringBuilder()
                .Append("[core]\n\trepositoryformatversion = 0\n")
                .Append($"[submodule \"plugins/{embeddedModule}\"]\n\tactive = true\n")
                .Append($"\turl = {DeclaredTestUrl(embeddedModule)}\n")
                .Append($"[submodule \"plugins/{divergentModule}\"]\n\tactive = true\n")
                .Append($"\turl = {PlantedLocalUrl}\n")
                .Append($"[submodule \"plugins/{rewrittenModule}\"]\n\tactive = true\n")
                .Append($"\turl = https://gitlab-ci-token:AbC-123@git.maxbrenne.de/clubgear/plugin-{rewrittenModule.ToLowerInvariant()}.git\n");

            Directory.CreateDirectory(Path.Combine(plantedRoot, ".git"));
            File.WriteAllText(Path.Combine(plantedRoot, ".git", "config"), effective.ToString());

            Directory.CreateDirectory(Path.Combine(plantedRoot, "plugins", embeddedModule, ".git"));
            Directory.CreateDirectory(Path.Combine(plantedRoot, "plugins", divergentModule));
            File.WriteAllText(
                Path.Combine(plantedRoot, "plugins", divergentModule, ".git"),
                $"gitdir: ../../.git/modules/plugins/{divergentModule}" + Environment.NewLine);

            var violations = FindSubmoduleHygieneViolations(plantedRoot);

            Assert.Equal(2, violations.Count);

            var embeddedViolation = Assert.Single(
                violations,
                violation => violation.Contains(EmbeddedRepositoryMarker, StringComparison.Ordinal));
            Assert.Contains($"plugins/{embeddedModule}", embeddedViolation, StringComparison.Ordinal);
            Assert.Contains(
                $"git submodule update --init plugins/{embeddedModule}",
                embeddedViolation,
                StringComparison.Ordinal);

            var urlViolation = Assert.Single(
                violations,
                violation => violation.Contains(DivergentUrlMarker, StringComparison.Ordinal));
            Assert.Contains($"plugins/{divergentModule}", urlViolation, StringComparison.Ordinal);
            Assert.Contains(PlantedLocalUrl, urlViolation, StringComparison.Ordinal);
            Assert.Contains(DeclaredTestUrl(divergentModule), urlViolation, StringComparison.Ordinal);
            Assert.Contains("git submodule sync --recursive", urlViolation, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plantedRoot, recursive: true);
        }
    }

    private static readonly IReadOnlyList<string> PluginModules = new[]
    {
        "CarInfo",
        "Finance",
        "IamOidc",
        "Inventar",
        "ServiceBook"
    };

    private static IReadOnlyList<string> FindMissingPluginModules(string repositoryRoot)
    {
        return PluginModules
            .Where(module => !Directory.Exists(Path.Combine(repositoryRoot, "plugins", module))
                             || !File.Exists(Path.Combine(repositoryRoot, "plugins", module, $"{module}.Plugin.csproj")))
            .ToArray();
    }

    private static string DescribeMissingModules(IReadOnlyList<string> missingModules)
    {
        return "Plugin submodules not checked out: "
               + string.Join(", ", missingModules)
               + ". Run 'git submodule update --init --recursive' before running the architecture tests.";
    }

    private static IEnumerable<string> EnumeratePluginSourceFiles(string repositoryRoot)
    {
        var pluginsFolder = Path.Combine(repositoryRoot, "plugins");

        if (!Directory.Exists(pluginsFolder))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(pluginsFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".cgcs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsUnderFolder(path, "bin") && !IsUnderFolder(path, "obj"))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static bool IsUnderFolder(string path, string folderName)
    {
        var segment = Path.DirectorySeparatorChar + folderName + Path.DirectorySeparatorChar;

        return path.Contains(segment, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> FindForbiddenReferences(string repositoryRoot, IEnumerable<string> sourceFiles)
    {
        var forbiddenTokens = PluginIsolationScanner.ForbiddenNamespaces
            .Append(nameof(ApplicationDbContext))
            .ToArray();

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var token in forbiddenTokens)
                {
                    if (!lines[index].Contains(token, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(
                        $"{Path.GetRelativePath(repositoryRoot, file)} line {index + 1} references '{token}'. "
                        + "Plugins may only use ClubGear.Plugin.Contracts.");
                }
            }
        }

        return violations;
    }

    private const string PlantedLocalUrl = "/Users/max/Code/ClubGear/plugins/Finance";

    private static string DeclaredTestUrl(string module)
    {
        return $"git@git.maxbrenne.de:clubgear/plugin-{module.ToLowerInvariant()}.git";
    }

    private const string EmbeddedRepositoryMarker = "embedded repository";
    private const string DivergentUrlMarker = "does not resolve to the declared";
    private const string UndeclaredModuleMarker = "not declared in .gitmodules";

    private const string SubmoduleUrlVectorFixture =
        "tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv";

    public static TheoryData<string, string> SubmoduleUrlNormalizationVectors()
    {
        var fixturePath = Path.Combine(
            ResolveRepositoryRoot(),
            SubmoduleUrlVectorFixture.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Shared normalisation vectors missing: {SubmoduleUrlVectorFixture}.", fixturePath);
        }

        var vectors = new TheoryData<string, string>();
        var lines = File.ReadAllLines(fixturePath);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split('\t');

            if (fields.Length != 2 || fields[0].Length == 0 || fields[1].Length == 0)
            {
                throw new InvalidDataException(
                    $"{SubmoduleUrlVectorFixture} line {index + 1} is not '<raw>\\t<expected>': '{line}'.");
            }

            vectors.Add(fields[0], fields[1]);
        }

        if (vectors.Count == 0)
        {
            throw new InvalidDataException($"{SubmoduleUrlVectorFixture} contains no cases.");
        }

        return vectors;
    }

    /// <summary>
    /// The ordered, total normalisation rule shared with the <c>normalize()</c> shell function in the
    /// <c>submodules</c> validate job of <c>.gitlab-ci.yml</c>. Pure: no environment, filesystem or network
    /// input. Both implementations are pinned to
    /// <c>Fixtures/submodule-url-normalization.cases.tsv</c>, which is what makes them provably agree.
    /// </summary>
    private static string NormalizeRemoteUrl(string rawUrl)
    {
        // 1. Trim leading/trailing whitespace.
        var value = rawUrl.Trim();

        // 2. Remove one trailing '/' if present.
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        // 3. Remove one trailing '.git' if present.
        if (value.EndsWith(".git", StringComparison.Ordinal))
        {
            value = value[..^".git".Length];
        }

        // 4. Delete everything up to and including the first '://'.
        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        var hadScheme = schemeIndex >= 0;

        if (hadScheme)
        {
            value = value[(schemeIndex + "://".Length)..];
        }

        // Steps 5 and 6 are scoped to the segment before the first '/', so a colon or an '@' further
        // along a filesystem or URL path can never be rewritten.
        var firstSlash = value.IndexOf('/');
        var segment = firstSlash < 0 ? value : value[..firstSlash];
        var remainder = firstSlash < 0 ? string.Empty : value[firstSlash..];

        // 5. Strip credentials: delete up to and including the LAST '@' in that segment, because the
        //    CI HTTPS token may itself contain '@'.
        var lastAt = segment.LastIndexOf('@');

        if (lastAt >= 0)
        {
            segment = segment[(lastAt + 1)..];
        }

        // 6. Rewrite the scp-style 'host:path' separator to '/'. Only when no '://' scheme was present:
        //    with a scheme the colon is an explicit port, which is a real difference and must survive
        //    (see the 'ssh://…:22/…' vector, which is documented as NOT equal to the bare form).
        var colonIndex = segment.IndexOf(':');

        if (!hadScheme && colonIndex >= 0)
        {
            segment = string.Concat(segment[..colonIndex], "/", segment[(colonIndex + 1)..]);
        }

        value = segment + remainder;

        // 7. Collapse any run of two or more '/' into one.
        var collapsed = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character == '/' && collapsed.Length > 0 && collapsed[^1] == '/')
            {
                continue;
            }

            collapsed.Append(character);
        }

        // 8. Lowercase (invariant).
        return collapsed.ToString().ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, string> ReadDeclaredSubmoduleUrls(string repositoryRoot)
    {
        return ReadSubmoduleUrls(Path.Combine(repositoryRoot, ".gitmodules"));
    }

    private static IReadOnlyDictionary<string, string> ReadEffectiveSubmoduleUrls(string repositoryRoot)
    {
        return ReadSubmoduleUrls(Path.Combine(repositoryRoot, ".git", "config"));
    }

    /// <summary>
    /// Minimal reader for the <c>[submodule "&lt;name&gt;"] url = …</c> pairs of a git config file. Reading
    /// the files instead of shelling out to <c>git</c> is deliberate: it is the only way the C# and the
    /// shell implementation see literally the same input.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ReadSubmoduleUrls(string configFile)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(configFile))
        {
            return urls;
        }

        string? currentSubmodule = null;

        foreach (var rawLine in File.ReadAllLines(configFile))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                currentSubmodule = ParseSubmoduleSectionName(line);
                continue;
            }

            if (currentSubmodule is null)
            {
                continue;
            }

            var separator = line.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            if (!line[..separator].Trim().Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            urls[currentSubmodule] = line[(separator + 1)..].Trim();
        }

        return urls;
    }

    private static string? ParseSubmoduleSectionName(string sectionLine)
    {
        const string prefix = "[submodule \"";

        if (!sectionLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var closingQuote = sectionLine.LastIndexOf('"');

        return closingQuote > prefix.Length ? sectionLine[prefix.Length..closingQuote] : null;
    }

    /// <summary>
    /// CORE-23. Composes the three inputs — <c>.gitmodules</c>, <c>.git/config</c> and the entry type of
    /// <c>plugins/&lt;X&gt;/.git</c> — into actionable violation strings. Takes the root so the planted-root
    /// test can drive it without a real defect, exactly as <see cref="FindMissingPluginModules"/> does.
    /// </summary>
    private static IReadOnlyList<string> FindSubmoduleHygieneViolations(string repositoryRoot)
    {
        var declaredUrls = ReadDeclaredSubmoduleUrls(repositoryRoot);
        var effectiveUrls = ReadEffectiveSubmoduleUrls(repositoryRoot);
        var violations = new List<string>();

        foreach (var module in PluginModules)
        {
            var submoduleName = "plugins/" + module;
            var gitEntry = Path.Combine(repositoryRoot, "plugins", module, ".git");

            if (Directory.Exists(gitEntry))
            {
                violations.Add(
                    $"{submoduleName}: {EmbeddedRepositoryMarker} — plugins/{module}/.git is a directory, "
                    + "expected a gitlink file. Remedy: verify nothing is unpushed, then "
                    + $"git submodule deinit -f plugins/{module}, remove plugins/{module}/.git, then "
                    + $"git submodule update --init plugins/{module}.");
            }

            if (!declaredUrls.TryGetValue(submoduleName, out var declaredUrl))
            {
                violations.Add($"{submoduleName}: {UndeclaredModuleMarker}.");
                continue;
            }

            // Absent from .git/config means the submodule is not initialised, which
            // PluginSubmodules_ShouldBe_CheckedOut already owns. Reporting it here too would make one
            // defect fail two tests and muddy the message.
            if (!effectiveUrls.TryGetValue(submoduleName, out var effectiveUrl))
            {
                continue;
            }

            if (!string.Equals(
                    NormalizeRemoteUrl(declaredUrl),
                    NormalizeRemoteUrl(effectiveUrl),
                    StringComparison.Ordinal))
            {
                violations.Add(
                    $"{submoduleName}: effective URL '{effectiveUrl}' {DivergentUrlMarker} "
                    + $"'{declaredUrl}'. Remedy: git submodule sync --recursive.");
            }
        }

        return violations;
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "clubgear-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        return root;
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var projectFile = Path.Combine(directory.FullName, "ClubGear.csproj");
            if (File.Exists(projectFile))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root from test execution path.");
    }
}