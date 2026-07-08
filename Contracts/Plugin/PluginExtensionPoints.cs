namespace ClubGear.Plugin.Contracts;

public static class PluginExtensionPoints
{
    public const string MemberDetail       = "member.detail";
    public const string MemberEdit         = "member.edit";
    public const string MemberBadge        = "member.badge";
    public const string MemberAction       = "member.action";
    public const string SelfServiceProfile = "selfservice.profile";
    public const string AdminFunctions     = "admin.functions";
    public const string RuntimeRoute       = "runtime.route";
    public const string NavMain            = "nav.main";
    public const string PageGeneric        = "page.generic";
    public const string AuditSink          = "audit.sink";
    public const string IdentityProvider   = "identity.provider";

    private static readonly HashSet<string> KnownValuesInternal = new(StringComparer.Ordinal)
    {
        MemberDetail, MemberEdit, MemberBadge, MemberAction,
        SelfServiceProfile, AdminFunctions, RuntimeRoute,
        NavMain, PageGeneric, AuditSink, IdentityProvider
    };

    public static IReadOnlySet<string> All => KnownValuesInternal;

    public static bool IsKnown(string value) => KnownValuesInternal.Contains(value);
}
