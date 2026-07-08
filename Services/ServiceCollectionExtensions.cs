using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Channels;
using ClubGear.Services.Core;
using ClubGear.Services.Core.SeedTasks;
using ClubGear.Services.Plugins.Catalog;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Manifest;
using ClubGear.Services.Plugins;
using ClubGear.Services.Plugins.Admin;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Security;
using ClubGear.Services.Plugins.Status;
using ClubGear.Services.Plugins.AuditSink;
using ClubGear.Services.ExternalLogin;
using ClubGear.Services.Plugins.Uninstall;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace ClubGear.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClubGearCoreServices(this IServiceCollection services)
    {
        services.AddScoped<DatabaseAuditLogService>();
        services.AddScoped<IAuditLogService>(sp =>
            new AuditSinkDispatchDecorator(
                sp.GetRequiredService<DatabaseAuditLogService>(),
                sp.GetRequiredService<IPluginAuditSinkService>(),
                sp.GetRequiredService<ILogger<AuditSinkDispatchDecorator>>()));
        services.AddScoped<IPluginAuditSinkService, PluginAuditSinkService>();
        services.AddScoped<IEventLogService, DatabaseEventLogService>();
        services.AddScoped<IPermissionService, DatabasePermissionService>();
        services.AddScoped<ITemplateRenderer, SimpleTemplateRenderer>();
        services.AddScoped<IMessageComposer, MessageComposer>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddSingleton<IProfileImageStorageService, ProfileImageStorageService>();
        services.AddScoped<IMemberFeatureService, MemberFeatureService>();
        services.AddSingleton<IMemberMetadataService, MemberMetadataService>();
        services.AddScoped<IMemberPluginSlotService, MemberPluginSlotService>();
        services.AddScoped<IPluginAdminCommandService, PluginAdminCommandService>();
        services.AddScoped<IAccountFeatureService, AccountFeatureService>();
        services.AddScoped<ISelfServiceFeatureService, SelfServiceFeatureService>();
        services.AddScoped<ISelfServiceSectionService, SelfServiceSectionService>();
        services.AddScoped<IExtensionPermissionFacade, ExtensionPermissionFacade>();
        services.AddScoped<IExtensionAuditFacade, ExtensionAuditFacade>();
        services.AddScoped<IExtensionNotificationFacade, ExtensionNotificationFacade>();
        services.AddScoped<IPluginRuntimeAdapter, PluginRuntimeAdapter>();
        services.AddSingleton<IPluginRuntimeRegistry, PluginRegistry>();
        services.AddSingleton<IPluginRegistryReader>(sp => sp.GetRequiredService<IPluginRuntimeRegistry>());
        services.AddSingleton<IPluginBackgroundJobRunner, PluginBackgroundJobRunner>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton(sp => new PluginEndpointRegistrar(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IPluginRuntimeRegistry>()));
        services.AddSingleton<IContractCompatibilityService, ContractCompatibilityService>();
        services.AddSingleton<PluginManifestParser>();
        services.AddSingleton<IPluginIntegrityVerifier, PluginIntegrityVerifier>();
        services.AddSingleton<IPluginPackageStore, FileSystemPluginPackageStore>();
        services.AddHttpClient<IPluginPackageDownloader, HttpPluginPackageDownloader>();
        services.AddSingleton<PluginSchemaNamePolicy>();
        services.AddScoped<IPluginStatusStore, DbPluginStatusStore>();
        services.AddScoped<IPluginAdminQueryService, PluginAdminQueryService>();
        services.AddScoped<PluginMigrationRunner>();
        services.AddScoped<IPluginLifecycleService, PluginLifecycleService>();
        services.AddSingleton<IPluginCatalogProvider>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var endpoint = configuration["Plugins:MarketplaceCatalogUrl"]
                ?? "https://plugins.clubgear.local/catalog";
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MarketplacePluginCatalogProvider));
            return new MarketplacePluginCatalogProvider(httpClient, endpoint);
        });
        services.AddScoped<IPluginInstallerService, PluginInstallerService>();
        services.AddScoped<IPluginUninstallService, PluginUninstallService>();
        services.AddScoped<IPluginNavEntryService, PluginNavEntryService>();
        services.AddScoped<IPluginPageService, PluginPageService>();

        services.AddScoped<INotificationChannel, SmtpEmailNotificationChannel>();
        services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        services.AddTransient<IConfigureNamedOptions<OpenIdConnectOptions>, OidcOptionsReloader>();
        services.AddScoped<IExternalLoginConfigService, ExternalLoginConfigService>();
        services.AddScoped<IIdpClaimsEnricher, IdpClaimsEnricher>();
        services.AddScoped<IExternalLoginInfoProvider, SignInManagerExternalLoginInfoProvider>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddScoped<IPermissionDefinitionProvider, CorePermissionDefinitionProvider>();

        services.AddScoped<ISeedTask, MemberSeedTask>();
        services.AddScoped<ISeedTask, PermissionSeedTask>();
        services.AddScoped<ISeedTask, RolePermissionSeedTask>();
        services.AddScoped<ISeedTask, SystemConfigSeedTask>();
        services.AddScoped<ISeedTask, MembershipTypePermissionSeedTask>();
        services.AddScoped<IApplicationSeeder, ApplicationSeeder>();

        services.AddScoped<IMembershipTypeService, MembershipTypeService>();

        return services;
    }
}
