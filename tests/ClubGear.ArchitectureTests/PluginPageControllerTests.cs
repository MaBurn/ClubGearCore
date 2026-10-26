using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClubGear.Controllers;
using ClubGear.Controllers.Api;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginPageControllerTests
{
    // ----------------------------------------------------------------
    // MVC controller tests
    // ----------------------------------------------------------------

    [Fact]
    public async Task Index_ReturnsView_WithRows()
    {
        var definition = BuildDefinition();
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
        [
            new Dictionary<string, string?> { ["Id"] = "1", ["Name"] = "Alpha" }
        ];

        var service = new StubPageService
        {
            DefinitionResult = PluginPageResult<PluginPageDefinition>.Success(definition),
            RowsResult = PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Success(rows)
        };

        using var controller = CreateMvcController(service, "page.read");
        var result = await controller.Index("test.module", "test.page");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public async Task Index_ReturnsForbid_WhenServiceForbids()
    {
        var service = new StubPageService
        {
            DefinitionResult = PluginPageResult<PluginPageDefinition>.Forbidden()
        };

        using var controller = CreateMvcController(service);
        var result = await controller.Index("test.module", "test.page");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsNotFound_WhenServiceReturnsNotFound()
    {
        var service = new StubPageService
        {
            DefinitionResult = PluginPageResult<PluginPageDefinition>.NotFound()
        };

        using var controller = CreateMvcController(service);
        var result = await controller.Index("test.module", "test.page");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsView_WhenRowFound()
    {
        var definition = BuildDefinition();
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
        [
            new Dictionary<string, string?> { ["Id"] = "42", ["Name"] = "Test Item" }
        ];

        var service = new StubPageService
        {
            DefinitionResult = PluginPageResult<PluginPageDefinition>.Success(definition),
            RowsResult = PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Success(rows)
        };

        using var controller = CreateMvcController(service, "page.read");
        var result = await controller.Detail("test.module", "test.page", "42");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenNoRows()
    {
        var definition = BuildDefinition();
        IReadOnlyList<IReadOnlyDictionary<string, string?>> emptyRows = Array.Empty<IReadOnlyDictionary<string, string?>>();

        var service = new StubPageService
        {
            DefinitionResult = PluginPageResult<PluginPageDefinition>.Success(definition),
            RowsResult = PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Success(emptyRows)
        };

        using var controller = CreateMvcController(service, "page.read");
        var result = await controller.Detail("test.module", "test.page", "999");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void IndexView_SupportsRowScopedCommandsWithPresetArguments()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "PluginPage", "Index.cshtml"));

        Assert.Contains("AvailableCommands", content, StringComparison.Ordinal);
        Assert.Contains("CommandArguments", content, StringComparison.Ordinal);
        Assert.Contains("data-command-arguments", content, StringComparison.Ordinal);
        Assert.Contains("availableCommands.Contains(cmd.Key)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailView_SupportsRowScopedCommandsWithPresetArguments()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "PluginPage", "Detail.cshtml"));

        Assert.Contains("AvailableCommands", content, StringComparison.Ordinal);
        Assert.Contains("CommandArguments", content, StringComparison.Ordinal);
        Assert.Contains("data-command-arguments", content, StringComparison.Ordinal);
        Assert.Contains("availableCommands.Contains(cmd.Key)", content, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // API controller tests
    // ----------------------------------------------------------------

    [Fact]
    public async Task ApiController_ReturnsOk_OnSuccess()
    {
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.Success(
                new PluginCommandResult(true, "created", "OK"))
        };

        var controller = CreateApiController(service, "page.manage");
        var request = new PluginPageCommandRequest("test.module", "test.page", "create");
        var result = await controller.ExecuteCommand(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PluginCommandResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("created", payload.Status);
    }

    [Fact]
    public async Task ApiController_ReturnsForbid_OnForbidden()
    {
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.Forbidden()
        };

        var controller = CreateApiController(service);
        var request = new PluginPageCommandRequest("test.module", "test.page", "create");
        var result = await controller.ExecuteCommand(request);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task ApiController_ReturnsNotFound_WhenServiceReturnsNotFound()
    {
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.NotFound()
        };

        var controller = CreateApiController(service);
        var request = new PluginPageCommandRequest("test.module", "test.page", "create");
        var result = await controller.ExecuteCommand(request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ApiController_ReturnsBadRequest_WhenCommandFails()
    {
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.Success(
                new PluginCommandResult(false, "validation-error", "Required field missing."))
        };

        var controller = CreateApiController(service, "page.manage");
        var request = new PluginPageCommandRequest("test.module", "test.page", "create");
        var result = await controller.ExecuteCommand(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<PluginCommandResult>(bad.Value);
        Assert.False(payload.Success);
    }

    [Fact]
    public async Task ApiController_ReturnsOk_WithFilePayload_WhenCommandDeliversFile()
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>"));
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.Success(
                new PluginCommandResult(
                    true,
                    "exported",
                    "OK",
                    FieldErrors: null,
                    File: new PluginCommandFile("sepa-einzug-2026-07.xml", "application/xml", base64)))
        };

        var controller = CreateApiController(service, "page.manage");
        var request = new PluginPageCommandRequest("test.module", "test.page", "export");
        var result = await controller.ExecuteCommand(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PluginCommandResult>(ok.Value);
        Assert.NotNull(payload.File);
        Assert.Equal("sepa-einzug-2026-07.xml", payload.File!.FileName);
        Assert.Equal("application/xml", payload.File.ContentType);
        Assert.Equal(base64, payload.File.ContentBase64);

        // The API serializes with the web (camelCase) defaults; the modal reads
        // result.file.contentBase64 etc., so the wire shape must match.
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"file\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fileName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"contentType\"", json, StringComparison.Ordinal);
        Assert.Contains("\"contentBase64\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiController_ReturnsOk_WithNullFile_ForFilelessCommand()
    {
        var service = new StubPageService
        {
            CommandResult = PluginPageResult<PluginCommandResult>.Success(
                new PluginCommandResult(true, "created", "OK"))
        };

        var controller = CreateApiController(service, "page.manage");
        var request = new PluginPageCommandRequest("test.module", "test.page", "create");
        var result = await controller.ExecuteCommand(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<PluginCommandResult>(ok.Value);
        Assert.Null(payload.File);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"file\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionModalView_DecodesFilePayloadAndSkipsReloadForFileResults()
    {
        var content = File.ReadAllText(
            GetProjectFilePath("Views", "PluginPage", "_PluginPageActionModal.cshtml"));

        Assert.Contains("tryDownloadResultFile", content, StringComparison.Ordinal);
        Assert.Contains("result.file", content, StringComparison.Ordinal);
        Assert.Contains("contentBase64", content, StringComparison.Ordinal);
        Assert.Contains("new Blob(", content, StringComparison.Ordinal);
        Assert.Contains("URL.createObjectURL", content, StringComparison.Ordinal);
        Assert.Contains("URL.revokeObjectURL", content, StringComparison.Ordinal);
        Assert.Contains("anchor.download", content, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static PluginPageDefinition BuildDefinition() =>
        new PluginPageDefinition(
            "test.page",
            "Test Page",
            "Id",
            [new PluginPageColumn("Id", "ID"), new PluginPageColumn("Name", "Name")],
            [new PluginPageCommand("create", "Erstellen", "bi-plus", "page.manage", null, false)],
            "page.read",
            "Suchen...");

    private static ClaimsPrincipal BuildUser(params string[] permissions)
    {
        var claims = permissions
            .Select(p => new Claim("permission", p))
            .ToList();
        claims.Add(new Claim(ClaimTypes.Name, "test-user"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
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

    private static PluginPageController CreateMvcController(IPluginPageService service, params string[] permissions)
    {
        return new PluginPageController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser(permissions)
                }
            }
        };
    }

    private static PluginPageApiController CreateApiController(IPluginPageService service, params string[] permissions)
    {
        return new PluginPageApiController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser(permissions)
                }
            }
        };
    }

    // ----------------------------------------------------------------
    // Stub service
    // ----------------------------------------------------------------

    private sealed class StubPageService : IPluginPageService
    {
        public PluginPageResult<PluginPageDefinition> DefinitionResult { get; set; }
            = PluginPageResult<PluginPageDefinition>.NotFound();

        public PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>> RowsResult { get; set; }
            = PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.NotFound();

        public PluginPageResult<PluginCommandResult> CommandResult { get; set; }
            = PluginPageResult<PluginCommandResult>.NotFound();

        public Task<PluginPageResult<PluginPageDefinition>> GetPageDefinitionAsync(
            string moduleId,
            string pageKey,
            ClaimsPrincipal user,
            CancellationToken ct = default)
            => Task.FromResult(DefinitionResult);

        public Task<PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>> GetRowsAsync(
            string moduleId,
            string pageKey,
            ClaimsPrincipal user,
            string? filterValue,
            string? entityKey,
            CancellationToken ct = default)
            => Task.FromResult(RowsResult);

        public Task<PluginPageResult<PluginCommandResult>> ExecuteCommandAsync(
            string moduleId,
            string pageKey,
            string commandKey,
            string? entityKey,
            IReadOnlyDictionary<string, string> arguments,
            ClaimsPrincipal user,
            CancellationToken ct = default)
            => Task.FromResult(CommandResult);

        /// <summary>The query the controller built, so a test can assert on what it passed on.</summary>
        public PluginPageQuery? LastQuery { get; private set; }

        /// <summary>Set to take over completely; otherwise <see cref="RowsResult"/> is mirrored.</summary>
        public PluginPageResult<PluginPageRows>? PageRowsResult { get; set; }

        public Task<PluginPageResult<PluginPageRows>> GetRowsPageAsync(
            string moduleId,
            string pageKey,
            ClaimsPrincipal user,
            PluginPageQuery query,
            CancellationToken ct = default)
        {
            LastQuery = query;

            if (PageRowsResult is not null)
            {
                return Task.FromResult(PageRowsResult);
            }

            // Mirror whatever RowsResult expresses so the tests written before 1.11.0 keep
            // exercising the same success / forbidden / not-found branches of the controller.
            if (RowsResult.IsForbidden)
            {
                return Task.FromResult(PluginPageResult<PluginPageRows>.Forbidden());
            }

            if (RowsResult.IsNotFound)
            {
                return Task.FromResult(PluginPageResult<PluginPageRows>.NotFound());
            }

            if (!RowsResult.IsSuccess)
            {
                return Task.FromResult(PluginPageResult<PluginPageRows>.Error(
                    RowsResult.ErrorMessage ?? "error"));
            }

            var rows = RowsResult.Value ?? [];
            return Task.FromResult(PluginPageResult<PluginPageRows>.Success(
                new PluginPageRows(rows, rows.Count)));
        }
    }
}
