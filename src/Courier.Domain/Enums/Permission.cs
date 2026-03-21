namespace Courier.Domain.Enums;

/// <summary>
/// Defines every authorized action in the system. Each value maps to a specific
/// resource + action from the RBAC permission matrix (Design Doc Section 12.2).
/// Used by RequirePermissionAttribute on controllers and by the frontend usePermissions hook.
/// </summary>
public enum Permission
{
    // Jobs
    JobsView,
    JobsCreate,
    JobsEdit,
    JobsDelete,
    JobsExecute,
    JobsManageSchedules,
    JobsManageDependencies,

    // Chains
    ChainsView,
    ChainsCreate,
    ChainsEdit,
    ChainsDelete,
    ChainsExecute,
    ChainsManageSchedules,

    // Connections
    ConnectionsView,
    ConnectionsCreate,
    ConnectionsEdit,
    ConnectionsDelete,
    ConnectionsTest,

    // PGP Keys
    PgpKeysView,
    PgpKeysManage,
    PgpKeysExportPublic,
    PgpKeysManageSharing,

    // SSH Keys
    SshKeysView,
    SshKeysManage,
    SshKeysExportPublic,
    SshKeysManageSharing,

    // File Monitors
    MonitorsView,
    MonitorsCreate,
    MonitorsEdit,
    MonitorsDelete,
    MonitorsChangeState,

    // Tags
    TagsView,
    TagsManage,

    // Notifications
    NotificationRulesView,
    NotificationRulesManage,
    NotificationLogsView,

    // Audit
    AuditLogView,

    // Users
    UsersView,
    UsersManage,

    // Settings
    SettingsView,
    SettingsManage,

    // Dashboard
    DashboardView,

    // Filesystem
    FilesystemBrowse,

    // Known Hosts
    KnownHostsView,
    KnownHostsManage,

    // Auth Providers
    AuthProvidersView,
    AuthProvidersCreate,
    AuthProvidersEdit,
    AuthProvidersDelete,
}
