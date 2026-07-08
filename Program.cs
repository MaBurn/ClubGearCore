using ClubGear.Data;
using ClubGear.Middleware;
using ClubGear.Models;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddClubGearCoreServices();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication()
    .AddOpenIdConnect("oidc.generic", "External IdP", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        // Placeholder so OIDC middleware doesn't reject startup validation.
        // OidcOptionsReloader overwrites this at request time from SystemConfigEntry.
        options.ClientId = "unconfigured";
        options.Authority = "https://unconfigured.invalid";
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IApplicationSeeder>();
    await seeder.SeedAsync();

    var pluginLifecycleService = scope.ServiceProvider.GetRequiredService<IPluginLifecycleService>();
    await pluginLifecycleService.LoadActivatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "member-singular-forward",
    pattern: "Member/{action=Index}/{id?}",
    defaults: new { controller = "Members" });

app.MapControllerRoute(
    name: "self-service-hyphen-forward",
    pattern: "Self-Service/{action=Index}/{id?}",
    defaults: new { controller = "SelfService" });

app.MapGet("/api/members", context =>
{
    context.Response.Redirect("/api/member", permanent: false);
    return Task.CompletedTask;
});

app.MapGet("/api/members/{id:int}", (HttpContext context, int id) =>
{
    context.Response.Redirect($"/api/member/{id}", permanent: false);
    return Task.CompletedTask;
});

app.Run();

// Expose Program for WebApplicationFactory in integration tests.
public partial class Program { }
