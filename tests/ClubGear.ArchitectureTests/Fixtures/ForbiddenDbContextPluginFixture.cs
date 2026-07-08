using ClubGear.Data;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.ArchitectureTests.ForbiddenPlugin;

public sealed class ForbiddenDbContextCapability
{
    private readonly ApplicationDbContext _dbContext;

    public ForbiddenDbContextCapability(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.CompletedTask;
}