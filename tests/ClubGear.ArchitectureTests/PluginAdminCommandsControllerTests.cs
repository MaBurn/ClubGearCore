using System.Security.Claims;
using ClubGear.Controllers.Api;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAdminCommandsControllerTests
{
    [Fact]
    public async Task GetPanels_ReturnsOk_WithResolvedPluginPanels()
    {
        var expected = new[]
        {
            new PluginAdminModulePanels(
                "plugin.runtime.admin",
                "Runtime Admin",
                [new PluginAdminPanel("vehicle-fields", "Feldverwaltung", "members.manage")])
        };
        var controller = CreateController(new FakePluginAdminCommandService
        {
            Panels = expected
        });

        var response = await controller.GetPanels();

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminModulePanels>>(ok.Value);
        var module = Assert.Single(payload);
        Assert.Equal("plugin.runtime.admin", module.ModuleId);
        Assert.Single(module.Panels);
    }

    [Theory]
    [InlineData("plugin-not-active", StatusCodes.Status404NotFound)]
    [InlineData("panel-not-found", StatusCodes.Status404NotFound)]
    [InlineData("command-not-found", StatusCodes.Status404NotFound)]
    [InlineData("forbidden", StatusCodes.Status403Forbidden)]
    public async Task Execute_MapsErrorStatusCodes(string status, int expectedStatusCode)
    {
        var controller = CreateController(new FakePluginAdminCommandService
        {
            Result = new PluginCommandResult(false, status, "error")
        });

        var response = await controller.Execute(new PluginAdminCommandExecutionRequest("plugin.runtime.admin", "vehicle-fields", "reindex"));

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
    }

    [Fact]
    public async Task Execute_ReturnsOk_WhenCommandSucceeds()
    {
        var controller = CreateController(new FakePluginAdminCommandService
        {
            Result = new PluginCommandResult(true, "executed", "ok")
        });

        var response = await controller.Execute(new PluginAdminCommandExecutionRequest("plugin.runtime.admin", "vehicle-fields", "reindex"));

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<PluginCommandResult>(ok.Value);
        Assert.True(payload.Success);
    }

    private static PluginAdminCommandsController CreateController(IPluginAdminCommandService service)
    {
        return new PluginAdminCommandsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin-tester")], "TestAuth"))
                }
            }
        };
    }

    private sealed class FakePluginAdminCommandService : IPluginAdminCommandService
    {
        public IReadOnlyList<PluginAdminModulePanels> Panels { get; set; } = Array.Empty<PluginAdminModulePanels>();
        public PluginCommandResult Result { get; set; } = new(false, "not-configured");

        public Task<IReadOnlyList<PluginAdminModulePanels>> GetPanelsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(Panels);

        public Task<PluginCommandResult> ExecuteCommandAsync(string moduleId, PluginAdminCommandRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);
    }
}
