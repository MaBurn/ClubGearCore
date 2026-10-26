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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ClubGear.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClubGearCoreServices(this IServiceCollection services)
    {
        services.AddScoped<DatabaseAuditLogService>();
        // CORE-09. TimeProvider is injected rather than DateTime.UtcNow so expiry and the
        // last-used throttle are testable.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // CORE-04. One substrate for notifications, chat and plugin-owned conversations.
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<ICurrentMemberAccessor, CurrentMemberAccessor>();

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
        services.AddScoped<IPluginPageColumnPreferenceService, PluginPageColumnPreferenceService>();
        services.AddSingleton<IProfileImageStorageService, ProfileImageStorageService>();
        services.AddScoped<IMemberFeatureService, MemberFeatureService>();
        services.AddScoped<IMemberEmailInvalidationService, MemberEmailInvalidationService>();
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
        // CORE-17. Singleton: deactivation drains the in-flight work counted here before it unloads.
        services.AddSingleton<IPluginActivityTracker, PluginActivityTracker>();
        services.AddOptions<PluginRuntimeOptions>()
            .Configure<IConfiguration>((options, configuration) => configuration.GetSection("Plugins").Bind(options));
        // CORE-13. Singleton because it holds no state of its own: it reads the runtime registry,
        // which is where activation and deactivation already take effect.
        services.AddSingleton<IPluginProviderRegistry, PluginProviderRegistry>();
        // CORE-18. Singleton: it owns no state either, it just builds a scope per lease and keeps
        // the module counted as busy for the lease's lifetime.
        services.AddSingleton<IPluginContributionActivator, PluginContributionActivator>();
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

        services.AddScoped<IImapMailboxService, ImapMailboxService>();
        // Inert while "BounceCheckEnabled" is off (the default): the poller wakes up, finds the
        // feature disabled, and goes back to sleep without touching the network.
        services.AddHostedService<BounceMailboxPoller>();
        services.AddScoped<INotificationChannel, SmtpEmailNotificationChannel>();
        services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IPostConfigureOptions<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>, OidcOptionsReloader>());
        services.AddScoped<IExternalLoginConfigService, ExternalLoginConfigService>();
        services.AddScoped<IIdpClaimsEnricher, IdpClaimsEnricher>();
        services.AddScoped<IExternalLoginInfoProvider, SignInManagerExternalLoginInfoProvider>();
        services.AddScoped<IExternalLoginSessionService, IdentityExternalLoginSessionService>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddScoped<IPermissionDefinitionProvider, CorePermissionDefinitionProvider>();

        services.AddScoped<ISeedTask, MemberSeedTask>();
        services.AddScoped<ISeedTask, DemoDataSeedTask>();
        services.AddScoped<ISeedTask, PermissionSeedTask>();
        services.AddScoped<ISeedTask, RolePermissionSeedTask>();
        services.AddScoped<ISeedTask, SystemConfigSeedTask>();
        services.AddScoped<ISeedTask, MembershipTypePermissionSeedTask>();
        services.AddScoped<IApplicationSeeder, ApplicationSeeder>();

        services.AddScoped<IMembershipTypeService, MembershipTypeService>();

        return services;
    }
}
