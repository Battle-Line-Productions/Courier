// Module-scope: Sets created once at import, not per render.
// This file mirrors the backend RolePermissions.cs — keep in sync.

export type Permission =
  | "JobsView" | "JobsCreate" | "JobsEdit" | "JobsDelete"
  | "JobsExecute" | "JobsManageSchedules" | "JobsManageDependencies"
  | "ChainsView" | "ChainsCreate" | "ChainsEdit" | "ChainsDelete"
  | "ChainsExecute" | "ChainsManageSchedules"
  | "ConnectionsView" | "ConnectionsCreate" | "ConnectionsEdit"
  | "ConnectionsDelete" | "ConnectionsTest"
  | "PgpKeysView" | "PgpKeysManage" | "PgpKeysExportPublic" | "PgpKeysManageSharing"
  | "SshKeysView" | "SshKeysManage" | "SshKeysExportPublic" | "SshKeysManageSharing"
  | "MonitorsView" | "MonitorsCreate" | "MonitorsEdit"
  | "MonitorsDelete" | "MonitorsChangeState"
  | "TagsView" | "TagsManage"
  | "NotificationRulesView" | "NotificationRulesManage" | "NotificationLogsView"
  | "AuditLogView"
  | "UsersView" | "UsersManage"
  | "SettingsView" | "SettingsManage"
  | "DashboardView"
  | "FilesystemBrowse"
  | "KnownHostsView" | "KnownHostsManage";

const allPermissions: Permission[] = [
  "JobsView", "JobsCreate", "JobsEdit", "JobsDelete",
  "JobsExecute", "JobsManageSchedules", "JobsManageDependencies",
  "ChainsView", "ChainsCreate", "ChainsEdit", "ChainsDelete",
  "ChainsExecute", "ChainsManageSchedules",
  "ConnectionsView", "ConnectionsCreate", "ConnectionsEdit",
  "ConnectionsDelete", "ConnectionsTest",
  "PgpKeysView", "PgpKeysManage", "PgpKeysExportPublic", "PgpKeysManageSharing",
  "SshKeysView", "SshKeysManage", "SshKeysExportPublic", "SshKeysManageSharing",
  "MonitorsView", "MonitorsCreate", "MonitorsEdit",
  "MonitorsDelete", "MonitorsChangeState",
  "TagsView", "TagsManage",
  "NotificationRulesView", "NotificationRulesManage", "NotificationLogsView",
  "AuditLogView",
  "UsersView", "UsersManage",
  "SettingsView", "SettingsManage",
  "DashboardView",
  "FilesystemBrowse",
  "KnownHostsView", "KnownHostsManage",
];

export const rolePermissions: Record<string, ReadonlySet<Permission>> = {
  admin: new Set(allPermissions),

  operator: new Set<Permission>([
    "JobsView", "JobsCreate", "JobsEdit", "JobsDelete",
    "JobsExecute", "JobsManageSchedules", "JobsManageDependencies",
    "ChainsView", "ChainsCreate", "ChainsEdit", "ChainsDelete",
    "ChainsExecute", "ChainsManageSchedules",
    "ConnectionsView", "ConnectionsTest",
    "PgpKeysView", "PgpKeysExportPublic",
    "SshKeysView", "SshKeysExportPublic",
    "MonitorsView", "MonitorsCreate", "MonitorsEdit",
    "MonitorsDelete", "MonitorsChangeState",
    "TagsView", "TagsManage",
    "NotificationRulesView", "NotificationRulesManage", "NotificationLogsView",
    "AuditLogView", "DashboardView", "SettingsView",
    "FilesystemBrowse", "KnownHostsView",
  ]),

  viewer: new Set<Permission>([
    "JobsView", "ChainsView", "ConnectionsView",
    "PgpKeysView", "PgpKeysExportPublic",
    "SshKeysView", "SshKeysExportPublic",
    "MonitorsView", "TagsView",
    "NotificationRulesView", "NotificationLogsView",
    "AuditLogView", "DashboardView", "SettingsView",
    "FilesystemBrowse", "KnownHostsView",
  ]),
};
