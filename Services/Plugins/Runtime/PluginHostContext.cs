using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginHostContext : IPluginHostContext
{
    public PluginHostContext(
        PluginManifest manifest,
        IMemberFeatureService memberFeatureService,
        IPluginDataStore persistence,
        Func<PluginMemberActionRequest, CancellationToken, Task<PluginMemberActionResult>>? executeMemberActionAsync = null,
        Func<string, CancellationToken, Task<bool>>? permissionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(memberFeatureService);
        ArgumentNullException.ThrowIfNull(persistence);

        Metadata = new PluginMetadataFacade(manifest);
        Members = new PluginMemberReader(memberFeatureService);
        MemberActions = new PluginMemberActionFacade(executeMemberActionAsync);
        Persistence = persistence;
        Permissions = permissionResolver is not null
            ? new PluginPermissionFacade(permissionResolver)
            : new NullPluginPermissionFacade();
    }

    public IPluginMetadataFacade Metadata { get; }

    public IPluginMemberReader Members { get; }

    public IPluginMemberActionFacade MemberActions { get; }

    public IPluginDataStore Persistence { get; }

    public IPluginPermissionFacade Permissions { get; }

    private sealed class PluginMetadataFacade : IPluginMetadataFacade
    {
        private readonly PluginManifest _manifest;

        public PluginMetadataFacade(PluginManifest manifest)
        {
            _manifest = manifest;
        }

        public PluginHostMetadata GetCurrent()
        {
            return new PluginHostMetadata(
                _manifest.ModuleId,
                _manifest.DisplayName,
                _manifest.License,
                _manifest.RequiredCoreVersion,
                _manifest.Permissions.ToArray(),
                _manifest.ExtensionPoints.ToArray());
        }
    }

    private sealed class PluginMemberReader : IPluginMemberReader
    {
        private readonly IMemberFeatureService _memberFeatureService;

        public PluginMemberReader(IMemberFeatureService memberFeatureService)
        {
            _memberFeatureService = memberFeatureService;
        }

        public async Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
        {
            var members = await _memberFeatureService.GetListAsync(search, cancellationToken);
            return members.Select(MapSummary).ToArray();
        }

        public async Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
        {
            var member = await _memberFeatureService.GetByIdAsync(memberId, cancellationToken);
            return member is null ? null : MapDetail(member);
        }

        private static PluginMemberSummary MapSummary(Member member)
        {
            return new PluginMemberSummary(
                member.Id,
                member.MemberNumber ?? string.Empty,
                member.FullName,
                member.IsActive,
                member.Email);
        }

        private static PluginMemberDetail MapDetail(Member member)
        {
            return new PluginMemberDetail(
                member.Id,
                member.MemberNumber ?? string.Empty,
                member.FullName,
                member.FirstName,
                member.LastName,
                member.Email,
                member.PhoneNumber,
                member.IsActive);
        }
    }

    private sealed class PluginMemberActionFacade : IPluginMemberActionFacade
    {
        private readonly Func<PluginMemberActionRequest, CancellationToken, Task<PluginMemberActionResult>>? _executeMemberActionAsync;

        public PluginMemberActionFacade(
            Func<PluginMemberActionRequest, CancellationToken, Task<PluginMemberActionResult>>? executeMemberActionAsync)
        {
            _executeMemberActionAsync = executeMemberActionAsync;
        }

        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (_executeMemberActionAsync is null)
            {
                return Task.FromResult(new PluginMemberActionResult(false, "not-supported", "Plugin-Mitgliedsaktionen sind im aktuellen Host-Kontext nicht verfuegbar."));
            }

            return _executeMemberActionAsync(request, cancellationToken);
        }
    }

    private sealed class PluginPermissionFacade : IPluginPermissionFacade
    {
        private readonly Func<string, CancellationToken, Task<bool>> _resolver;

        public PluginPermissionFacade(Func<string, CancellationToken, Task<bool>> resolver)
        {
            _resolver = resolver;
        }

        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => _resolver(permissionKey, cancellationToken);
    }

    private sealed class NullPluginPermissionFacade : IPluginPermissionFacade
    {
        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}