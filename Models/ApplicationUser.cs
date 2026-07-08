using Microsoft.AspNetCore.Identity;

namespace ClubGear.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
