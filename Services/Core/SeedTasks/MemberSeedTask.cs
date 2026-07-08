using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

public class MemberSeedTask : ISeedTask
{
    public int Order => 10;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.Members.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Members.Add(new Member
        {
            MemberNumber = "M-0001",
            FirstName = "Demo",
            LastName = "Mitglied",
            Email = "demo.member@clubgear.local",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
