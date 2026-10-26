using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Data;
using ClubGear.Services.Plugins.Persistence;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginRuntimeAdapter : IPluginRuntimeAdapter
{
    private readonly IExtensionPermissionFacade _permissionFacade;
    private readonly IExtensionAuditFacade _auditFacade;
    private readonly IExtensionNotificationFacade _notificationFacade;
    private readonly IMemberFeatureService _memberFeatureService;
    private readonly ApplicationDbContext? _dbContext;
    private readonly PluginSchemaNamePolicy? _schemaNamePolicy;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<PluginRuntimeAdapter> _logger;
    private readonly IPluginActivityTracker? _activityTracker;

    public PluginRuntimeAdapter(
        IExtensionPermissionFacade permissionFacade,
        IExtensionAuditFacade auditFacade,
        IExtensionNotificationFacade notificationFacade,
        IMemberFeatureService memberFeatureService,
        ILogger<PluginRuntimeAdapter> logger,
        ApplicationDbContext? dbContext = null,
        PluginSchemaNamePolicy? schemaNamePolicy = null,
        IServiceScopeFactory? scopeFactory = null,
        IPluginActivityTracker? activityTracker = null)
    {
        _permissionFacade = permissionFacade;
        _auditFacade = auditFacade;
        _notificationFacade = notificationFacade;
        _memberFeatureService = memberFeatureService;
        _dbContext = dbContext;
        _schemaNamePolicy = schemaNamePolicy;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _activityTracker = activityTracker;
    }

    public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);
        ArgumentNullException.ThrowIfNull(user);

        // Use a holder to resolve the circular-reference ordering:
        // PluginHostContext needs a permission resolver that delegates to the bridge,
        // but the bridge is constructed after the host context.
        // The lazy closure captures the holder, which is populated immediately after.
        PluginRuntimeBridge? bridgeHolder = null;

        var host = new PluginHostContext(
            pluginModule.Manifest,
            _memberFeatureService,
            CreateDataStore(pluginModule.Manifest),
            (request, cancellationToken) => ExecuteMemberActionAsync(pluginModule.Manifest.ModuleId, request, user, cancellationToken),
            (permissionKey, cancellationToken) => bridgeHolder!.HasPermissionAsync(permissionKey, cancellationToken));

        var bridge = new PluginRuntimeBridge(
            pluginModule,
            user,
            _permissionFacade,
            _auditFacade,
            _notificationFacade,
            host);

        bridgeHolder = bridge;
        return bridge;
    }

    private async Task<PluginMemberActionResult> ExecuteMemberActionAsync(
        string moduleId,
        PluginMemberActionRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (_scopeFactory is null)
        {
            return new PluginMemberActionResult(false, "not-supported", "Plugin-Mitgliedsaktionen sind in diesem Host-Kontext nicht verfuegbar.");
        }

        using var scope = _scopeFactory.CreateScope();
        var slotService = scope.ServiceProvider.GetService<IMemberPluginSlotService>();
        if (slotService is null)
        {
            return new PluginMemberActionResult(false, "not-supported", "Plugin-Mitgliedsaktionen sind in diesem Host-Kontext nicht verfuegbar.");
        }

        var mappedRequest = new MemberPluginActionRequestModel(moduleId, request.ActionKey, request.MemberId, request.Arguments);
        return await slotService.ExecuteActionAsync(mappedRequest, user, cancellationToken);
    }

    public async Task RunAsync(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(capability);

        EnsureIsolated(capability);

        // The token keeps deactivation waiting until this invocation is done (CORE-17).
        using var activity = AcquireActivity(pluginModule.Manifest.ModuleId);

        var bridge = CreateBridge(pluginModule, user);
        await capability(bridge, cancellationToken);
    }

    public async Task<TResult> InvokeAsync<TResult>(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
        string? requiredPermissionKey = null,
        Delegate? isolatedDelegate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(capability);

        EnsureIsolated(isolatedDelegate ?? capability);

        // The token keeps deactivation waiting until this invocation is done (CORE-17).
        // Plugin HTTP routes inherit this because they funnel through InvokeAsync.
        using var activity = AcquireActivity(pluginModule.Manifest.ModuleId);

        var bridge = CreateBridge(pluginModule, user);
        if (!string.IsNullOrWhiteSpace(requiredPermissionKey))
        {
            var isAllowed = await bridge.HasPermissionAsync(requiredPermissionKey, cancellationToken);
            if (!isAllowed)
            {
                throw new PluginPermissionDeniedException(pluginModule.Manifest.ModuleId, requiredPermissionKey);
            }
        }

        return await capability(bridge, cancellationToken);
    }

    private IDisposable? AcquireActivity(string moduleId)
    {
        if (_activityTracker is null)
        {
            return null;
        }

        var token = _activityTracker.TryAcquire(moduleId);
        if (token is null)
        {
            _logger.LogWarning(
                "Plugin-Aufruf abgelehnt, weil das Modul {PluginKey} gerade deaktiviert wird.",
                moduleId);

            throw new UserFriendlyException("Das Plugin wird gerade deaktiviert.");
        }

        return token;
    }

    public void EnsureIsolated(Delegate pluginDelegate)
    {
        ArgumentNullException.ThrowIfNull(pluginDelegate);

        var pluginType = pluginDelegate.Target?.GetType() ?? pluginDelegate.Method.DeclaringType;
        if (pluginType is null)
        {
            return;
        }

        if (!PluginIsolationScanner.TryFindViolation(pluginType, out var violation))
        {
            return;
        }

        _logger.LogWarning(
            "Direkter Core-Zugriff fuer Plugin-Handler blockiert. Typ: {PluginType}, Referenz: {Violation}",
            pluginType.FullName,
            violation);

        throw new UserFriendlyException("Direkter Zugriff auf Core-Namensraeume ist fuer Plugins nicht erlaubt.");
    }

    private IPluginDataStore CreateDataStore(PluginManifest manifest)
    {
        if (_dbContext is not null && _schemaNamePolicy is not null)
        {
            return new PluginDataStore(manifest.ModuleId, _dbContext, _schemaNamePolicy);
        }

        return new NullPluginDataStore(manifest.ModuleId, manifest.SuggestedDataPrefix);
    }

    private sealed class NullPluginDataStore : IPluginDataStore
    {
        public NullPluginDataStore(string moduleId, string tablePrefix)
        {
            ModuleId = moduleId;
            TablePrefix = tablePrefix;
        }

        public string ModuleId { get; }

        public string TablePrefix { get; }

        public string GetTableName(string localName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localName);
            return TablePrefix + localName;
        }

        public Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
            => throw new UserFriendlyException("Plugin-Persistenz ist in diesem Host-Kontext nicht verfuegbar.");

        public Task<IReadOnlyList<PluginDataRow>> QueryAsync(
            string sql,
            IReadOnlyDictionary<string, string?>? parameters = null,
            CancellationToken cancellationToken = default)
            => throw new UserFriendlyException("Plugin-Persistenz ist in diesem Host-Kontext nicht verfuegbar.");
    }

    private sealed class PluginRuntimeBridge : IPluginRuntimeBridge
    {
        private readonly IPluginModule _pluginModule;
        private readonly ClaimsPrincipal _user;
        private readonly IExtensionPermissionFacade _permissionFacade;
        private readonly IExtensionAuditFacade _auditFacade;
        private readonly IExtensionNotificationFacade _notificationFacade;
        private readonly IPluginHostContext _host;

        public PluginRuntimeBridge(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            IExtensionPermissionFacade permissionFacade,
            IExtensionAuditFacade auditFacade,
            IExtensionNotificationFacade notificationFacade,
            IPluginHostContext host)
        {
            _pluginModule = pluginModule;
            _user = user;
            _permissionFacade = permissionFacade;
            _auditFacade = auditFacade;
            _notificationFacade = notificationFacade;
            _host = host;
        }

        public string ModuleId => _pluginModule.Manifest.ModuleId;

        public IPluginHostContext Host => _host;

        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => _permissionFacade.HasPermissionAsync(
                _user,
                permissionKey,
                _pluginModule.Manifest.Permissions,
                cancellationToken);

        public Task LogAsync(
            string action,
            string? actor = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            var record = new AuditLogRecord(
                Action: action,
                Actor: actor,
                Source: $"plugin:{_pluginModule.Manifest.ModuleId}",
                TargetType: targetType,
                TargetId: targetId,
                Metadata: metadata);

            return _auditFacade.LogAsync(record, cancellationToken);
        }

        public async Task<PluginRuntimeNotificationResult> NotifyAsync(
            PluginRuntimeNotification notification,
            CancellationToken cancellationToken = default)
        {
            var message = new NotificationMessage(
                notification.Recipient,
                notification.Subject,
                notification.Body,
                notification.Channel,
                notification.CorrelationId,
                notification.Metadata);

            var result = await _notificationFacade.NotifyAsync(message, cancellationToken);
            return new PluginRuntimeNotificationResult(result.Success, result.Channel, result.Recipient, result.Error);
        }
    }
}