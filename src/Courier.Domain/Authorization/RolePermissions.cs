using System.Collections.Frozen;
using Courier.Domain.Enums;

namespace Courier.Domain.Authorization;

/// <summary>
/// Single source of truth for which roles have which permissions.
/// Maps directly to the permission matrix in Design Doc Section 12.2.
/// This class is intentionally in the Domain layer (BCL-only, no external deps).
/// </summary>
public static class RolePermissions
{
    private static readonly FrozenSet<Permission> AdminPermissions =
        Enum.GetValues<Permission>().ToFrozenSet();

    private static readonly FrozenSet<Permission> OperatorPermissions = new HashSet<Permission>
    {
        // Jobs: full operational access
        Permission.JobsView,
        Permission.JobsCreate,
        Permission.JobsEdit,
        Permission.JobsDelete,
        Permission.JobsExecute,
        Permission.JobsManageSchedules,
        Permission.JobsManageDependencies,

        // Chains: full operational access
        Permission.ChainsView,
        Permission.ChainsCreate,
        Permission.ChainsEdit,
        Permission.ChainsDelete,
        Permission.ChainsExecute,
        Permission.ChainsManageSchedules,

        // Connections: view + test only (no create/edit/delete)
        Permission.ConnectionsView,
        Permission.ConnectionsTest,

        // Keys: view + export public only (no manage/sharing)
        Permission.PgpKeysView,
        Permission.PgpKeysExportPublic,
        Permission.SshKeysView,
        Permission.SshKeysExportPublic,

        // Monitors: full operational access
        Permission.MonitorsView,
        Permission.MonitorsCreate,
        Permission.MonitorsEdit,
        Permission.MonitorsDelete,
        Permission.MonitorsChangeState,

        // Tags: full access
        Permission.TagsView,
        Permission.TagsManage,

        // Notifications: full access
        Permission.NotificationRulesView,
        Permission.NotificationRulesManage,
        Permission.NotificationLogsView,

        // Read-only shared resources
        Permission.AuditLogView,
        Permission.DashboardView,
        Permission.SettingsView,
        Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    private static readonly FrozenSet<Permission> ViewerPermissions = new HashSet<Permission>
    {
        // View-only across all resources
        Permission.JobsView,
        Permission.ChainsView,
        Permission.ConnectionsView,
        Permission.PgpKeysView,
        Permission.PgpKeysExportPublic,
        Permission.SshKeysView,
        Permission.SshKeysExportPublic,
        Permission.MonitorsView,
        Permission.TagsView,
        Permission.NotificationRulesView,
        Permission.NotificationLogsView,
        Permission.AuditLogView,
        Permission.DashboardView,
        Permission.SettingsView,
        Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    /// <summary>
    /// Returns the set of permissions for a given role name.
    /// Returns empty set for unknown roles (fail-closed).
    /// </summary>
    public static IReadOnlySet<Permission> GetPermissions(string role) => role.ToLowerInvariant() switch
    {
        "admin" => AdminPermissions,
        "operator" => OperatorPermissions,
        "viewer" => ViewerPermissions,
        _ => FrozenSet<Permission>.Empty,
    };

    /// <summary>
    /// Checks if a role has a specific permission.
    /// </summary>
    public static bool HasPermission(string role, Permission permission)
        => GetPermissions(role).Contains(permission);
}
