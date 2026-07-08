using System.Security.Claims;
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
    }
}
