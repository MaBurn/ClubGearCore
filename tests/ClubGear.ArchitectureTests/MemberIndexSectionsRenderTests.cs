using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberIndexSectionsRenderTests
{
    [Fact]
    public void MembersIndex_HasCreateMemberLink()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        Assert.Contains("asp-action=\"Create\"", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_HasImportLink()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        Assert.Contains("asp-action=\"Import\"", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_HasSearchInput()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        Assert.Contains("id=\"searchInput\"", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_HasMemberTable()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        Assert.Contains("member-table", indexContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MembersIndex_HasFeedbackArea()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        Assert.Contains("id=\"feedbackArea\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_FeedbackArea\"", indexContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_HeaderActions.cshtml")]
    [InlineData("_SearchAndFilters.cshtml")]
    [InlineData("_ListSegments.cshtml")]
    [InlineData("_BulkActions.cshtml")]
    [InlineData("_FeedbackArea.cshtml")]
    public void MembersIndex_SectionPartialsExistAndAreNotEmpty(string partialName)
    {
        var partialPath = GetProjectFilePath("Views", "Members", partialName);

        Assert.True(File.Exists(partialPath), $"Partial fehlt: {partialName}");

        var partialContent = File.ReadAllText(partialPath);
        Assert.False(string.IsNullOrWhiteSpace(partialContent));
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
