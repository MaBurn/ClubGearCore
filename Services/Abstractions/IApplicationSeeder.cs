using ClubGear.Data;

namespace ClubGear.Services.Abstractions;

public interface IApplicationSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public interface ISeedTask
{
    int Order { get; }
    Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
