using ClubGear.Services.Plugins.Manifest;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginManifestParserTests
{
    private readonly PluginManifestParser _sut = new();

    [Fact]
        public void Parse_ReturnsValidResult_ForCanonicalPluginJsonManifest()
    {
        var json =
            """
            {
                            "key": "clubgear.members.analytics",
                            "name": "Members Analytics",
                            "version": "1.2.0",
                            "author": "ClubGear",
                            "license": "Proprietary",
                            "category": "member-profile",
                            "entryPoint": "Members.Analytics.PluginModule",
                            "requiredCoreVersion": ">=1.0.0",
                            "permissions": ["Plugin_Analytics_View"],
                            "extensionPoints": ["member.detail", "member.edit"]
            }
            """;

        var result = _sut.Parse(json);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Manifest);
        Assert.Empty(result.Errors);
                Assert.Equal("clubgear.members.analytics", result.Manifest!.Key);
                Assert.Equal("Members Analytics", result.Manifest.Name);
                Assert.Equal(new Version(1, 2, 0), result.Manifest.Version);
                Assert.Equal("ClubGear", result.Manifest.Author);
                Assert.Equal("Proprietary", result.Manifest.License);
                Assert.Equal("Members.Analytics.PluginModule", result.Manifest.EntryPoint);
                Assert.Equal(">=1.0.0", result.Manifest.RequiredCoreVersion);
                Assert.Contains("Plugin_Analytics_View", result.Manifest.Permissions);
                Assert.Contains("member.detail", result.Manifest.ExtensionPoints);
                Assert.Equal("member-profile", result.Manifest.Category);
    }

    [Fact]
        public void Parse_ReturnsValidResult_ForLegacyManifestAlias()
        {
                var json =
                        """
                        {
                            "moduleId": "clubgear.members.analytics",
                            "displayName": "Members Analytics",
                            "pluginVersion": "1.2.0",
                            "requiredContractVersion": "1.0.0",
                            "entryPointType": "Members.Analytics.PluginModule"
                        }
                        """;

                var result = _sut.Parse(json);

                Assert.True(result.IsValid);
                Assert.NotNull(result.Manifest);
                Assert.Equal("clubgear.members.analytics", result.Manifest!.Key);
                Assert.Equal("Members Analytics", result.Manifest.Name);
                Assert.Equal("Unknown", result.Manifest.Author);
                Assert.Equal("Unspecified", result.Manifest.License);
                Assert.Equal("1.0.0", result.Manifest.RequiredCoreVersion);
                Assert.Empty(result.Manifest.Permissions);
                Assert.Empty(result.Manifest.ExtensionPoints);
                Assert.Equal("General", result.Manifest.Category);
        }

        [Fact]
        public void Parse_ReturnsInvalidResult_ForSchemaViolations()
    {
        var json =
            """
            {
                            "name": "Members Analytics",
                            "version": "not-a-version",
                            "author": "ClubGear",
                            "license": "Proprietary",
                            "entryPoint": "Members.Analytics.PluginModule",
                            "requiredCoreVersion": "^1.0.0",
                            "permissions": ["Plugin_Analytics_View"],
                            "extensionPoints": ["member.detail"]
            }
            """;

        var result = _sut.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Manifest);
        Assert.Contains(result.Errors, error => error.Contains("key"));
        Assert.Contains(result.Errors, error => error.Contains("version"));
        Assert.Contains(result.Errors, error => error.Contains("requiredCoreVersion"));
    }

    [Fact]
    public void Parse_ReturnsInvalidResult_ForUnknownExtensionPoint()
    {
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "category": "member-profile",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": ["member.detail", "unknown.slot"]
            }
            """;

        var result = _sut.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("unknown.slot"));
    }

    [Fact]
    public void Parse_ReturnsInvalidResult_ForAllUnknownExtensionPoints()
    {
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "category": "member-profile",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": ["vendor.custom", "other.fake"]
            }
            """;

        var result = _sut.Parse(json);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }

    [Fact]
    public void Parse_ReturnsValidResult_ForAllKnownExtensionPoints()
    {
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "category": "member-profile",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": [
                    "member.detail",
                    "member.edit",
                    "member.badge",
                    "member.action",
                    "selfservice.profile",
                    "admin.functions",
                    "runtime.route"
                ]
            }
            """;

        var result = _sut.Parse(json);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Manifest);
        Assert.Equal(7, result.Manifest!.ExtensionPoints.Count);
    }

    [Fact]
    public void Parse_ReturnsValidResult_ForEmptyExtensionPoints()
    {
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "category": "member-profile",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": []
            }
            """;

        var result = _sut.Parse(json);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Manifest);
        Assert.Empty(result.Manifest!.ExtensionPoints);
    }
}