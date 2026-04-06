import { describe, it, expect } from "vitest";
import { rolePermissions, type Permission } from "./permissions";

// Complete list of all permissions, mirroring the source module.
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
  "AuthProvidersView", "AuthProvidersCreate", "AuthProvidersEdit", "AuthProvidersDelete",
];

describe("rolePermissions", () => {
  describe("structure", () => {
    it("should define exactly three roles: admin, operator, viewer", () => {
      const roles = Object.keys(rolePermissions);
      expect(roles).toHaveLength(3);
      expect(roles).toContain("admin");
      expect(roles).toContain("operator");
      expect(roles).toContain("viewer");
    });

    it("should return undefined for an unknown role", () => {
      expect(rolePermissions["unknown"]).toBeUndefined();
      expect(rolePermissions["superadmin"]).toBeUndefined();
      expect(rolePermissions[""]).toBeUndefined();
    });

    it("should expose each role as a ReadonlySet", () => {
      for (const role of Object.keys(rolePermissions)) {
        expect(rolePermissions[role]).toBeInstanceOf(Set);
      }
    });
  });

  describe("admin role", () => {
    const admin = rolePermissions["admin"];

    it("should have every single permission", () => {
      for (const perm of allPermissions) {
        expect(admin.has(perm)).toBe(true);
      }
    });

    it("should have a permission count equal to the total number of permissions", () => {
      expect(admin.size).toBe(allPermissions.length);
      expect(admin.size).toBe(49);
    });

    it("should contain no permissions beyond the known set", () => {
      const adminPerms = [...admin];
      for (const perm of adminPerms) {
        expect(allPermissions).toContain(perm);
      }
    });
  });

  describe("operator role", () => {
    const operator = rolePermissions["operator"];

    it("should be a strict subset of admin", () => {
      const admin = rolePermissions["admin"];
      for (const perm of operator) {
        expect(admin.has(perm)).toBe(true);
      }
      expect(operator.size).toBeLessThan(admin.size);
    });

    it.each([
      "JobsView", "JobsCreate", "JobsEdit", "JobsDelete",
      "JobsExecute", "JobsManageSchedules", "JobsManageDependencies",
    ] satisfies Permission[])("should have job permission: %s", (perm) => {
      expect(operator.has(perm)).toBe(true);
    });

    it.each([
      "ChainsView", "ChainsCreate", "ChainsEdit", "ChainsDelete",
      "ChainsExecute", "ChainsManageSchedules",
    ] satisfies Permission[])("should have chain permission: %s", (perm) => {
      expect(operator.has(perm)).toBe(true);
    });

    it.each([
      "MonitorsView", "MonitorsCreate", "MonitorsEdit",
      "MonitorsDelete", "MonitorsChangeState",
    ] satisfies Permission[])("should have monitor permission: %s", (perm) => {
      expect(operator.has(perm)).toBe(true);
    });

    it.each([
      "TagsManage", "NotificationRulesManage", "FilesystemBrowse",
      "ConnectionsView", "ConnectionsTest",
      "PgpKeysView", "PgpKeysExportPublic",
      "SshKeysView", "SshKeysExportPublic",
      "TagsView", "NotificationRulesView", "NotificationLogsView",
      "AuditLogView", "DashboardView", "SettingsView",
      "KnownHostsView", "AuthProvidersView",
    ] satisfies Permission[])("should have permission: %s", (perm) => {
      expect(operator.has(perm)).toBe(true);
    });

    it.each([
      "UsersManage", "UsersView",
      "SettingsManage",
      "ConnectionsCreate", "ConnectionsEdit", "ConnectionsDelete",
      "PgpKeysManage", "PgpKeysManageSharing",
      "SshKeysManage", "SshKeysManageSharing",
      "AuthProvidersCreate", "AuthProvidersEdit", "AuthProvidersDelete",
      "KnownHostsManage",
    ] satisfies Permission[])("should NOT have admin-only permission: %s", (perm) => {
      expect(operator.has(perm)).toBe(false);
    });

    it("should have exactly 35 permissions", () => {
      expect(operator.size).toBe(35);
    });
  });

  describe("viewer role", () => {
    const viewer = rolePermissions["viewer"];

    it("should be a strict subset of operator", () => {
      const operator = rolePermissions["operator"];
      for (const perm of viewer) {
        expect(operator.has(perm)).toBe(true);
      }
      expect(viewer.size).toBeLessThan(operator.size);
    });

    it("should be a strict subset of admin", () => {
      const admin = rolePermissions["admin"];
      for (const perm of viewer) {
        expect(admin.has(perm)).toBe(true);
      }
      expect(viewer.size).toBeLessThan(admin.size);
    });

    it.each([
      "JobsView", "ChainsView", "ConnectionsView",
      "PgpKeysView", "PgpKeysExportPublic",
      "SshKeysView", "SshKeysExportPublic",
      "MonitorsView", "TagsView",
      "NotificationRulesView", "NotificationLogsView",
      "AuditLogView", "DashboardView", "SettingsView",
      "FilesystemBrowse", "KnownHostsView",
    ] satisfies Permission[])("should have view permission: %s", (perm) => {
      expect(viewer.has(perm)).toBe(true);
    });

    it.each([
      "JobsCreate", "JobsEdit", "JobsDelete", "JobsExecute",
      "JobsManageSchedules", "JobsManageDependencies",
      "ChainsCreate", "ChainsEdit", "ChainsDelete",
      "ChainsExecute", "ChainsManageSchedules",
      "ConnectionsCreate", "ConnectionsEdit", "ConnectionsDelete", "ConnectionsTest",
      "MonitorsCreate", "MonitorsEdit", "MonitorsDelete", "MonitorsChangeState",
      "TagsManage",
      "NotificationRulesManage",
      "UsersView", "UsersManage",
      "SettingsManage",
      "PgpKeysManage", "PgpKeysManageSharing",
      "SshKeysManage", "SshKeysManageSharing",
      "KnownHostsManage",
      "AuthProvidersCreate", "AuthProvidersEdit", "AuthProvidersDelete",
      "AuthProvidersView",
    ] satisfies Permission[])("should NOT have mutating permission: %s", (perm) => {
      expect(viewer.has(perm)).toBe(false);
    });

    it("should have exactly 16 permissions", () => {
      expect(viewer.size).toBe(16);
    });
  });

  describe("role hierarchy invariants", () => {
    it("admin should have more permissions than operator", () => {
      expect(rolePermissions["admin"].size).toBeGreaterThan(
        rolePermissions["operator"].size
      );
    });

    it("operator should have more permissions than viewer", () => {
      expect(rolePermissions["operator"].size).toBeGreaterThan(
        rolePermissions["viewer"].size
      );
    });

    it("every viewer permission should also exist in operator", () => {
      const viewer = rolePermissions["viewer"];
      const operator = rolePermissions["operator"];
      const missingFromOperator: Permission[] = [];
      for (const perm of viewer) {
        if (!operator.has(perm)) {
          missingFromOperator.push(perm);
        }
      }
      expect(missingFromOperator).toEqual([]);
    });

    it("every operator permission should also exist in admin", () => {
      const operator = rolePermissions["operator"];
      const admin = rolePermissions["admin"];
      const missingFromAdmin: Permission[] = [];
      for (const perm of operator) {
        if (!admin.has(perm)) {
          missingFromAdmin.push(perm);
        }
      }
      expect(missingFromAdmin).toEqual([]);
    });

    it("admin-only permissions should be exactly 14", () => {
      const admin = rolePermissions["admin"];
      const operator = rolePermissions["operator"];
      const adminOnly = [...admin].filter((p) => !operator.has(p));
      expect(adminOnly).toHaveLength(14);
      expect(adminOnly.sort()).toEqual([
        "AuthProvidersCreate",
        "AuthProvidersDelete",
        "AuthProvidersEdit",
        "ConnectionsCreate",
        "ConnectionsDelete",
        "ConnectionsEdit",
        "KnownHostsManage",
        "PgpKeysManage",
        "PgpKeysManageSharing",
        "SettingsManage",
        "SshKeysManage",
        "SshKeysManageSharing",
        "UsersManage",
        "UsersView",
      ]);
    });

    it("operator-only permissions (not in viewer) should be exactly 19", () => {
      const operator = rolePermissions["operator"];
      const viewer = rolePermissions["viewer"];
      const operatorOnly = [...operator].filter((p) => !viewer.has(p));
      expect(operatorOnly).toHaveLength(19);
      expect(operatorOnly.sort()).toEqual([
        "AuthProvidersView",
        "ChainsCreate",
        "ChainsDelete",
        "ChainsEdit",
        "ChainsExecute",
        "ChainsManageSchedules",
        "ConnectionsTest",
        "JobsCreate",
        "JobsDelete",
        "JobsEdit",
        "JobsExecute",
        "JobsManageDependencies",
        "JobsManageSchedules",
        "MonitorsChangeState",
        "MonitorsCreate",
        "MonitorsDelete",
        "MonitorsEdit",
        "NotificationRulesManage",
        "TagsManage",
      ].sort());
    });
  });

  describe("permission completeness", () => {
    it("allPermissions test fixture should have no duplicates", () => {
      const unique = new Set(allPermissions);
      expect(unique.size).toBe(allPermissions.length);
    });

    it("admin set should contain no duplicate entries", () => {
      // Sets inherently deduplicate, so size should match the array length
      // This validates the source array had no accidental duplicates
      const admin = rolePermissions["admin"];
      expect(admin.size).toBe(49);
    });

    it("every permission in allPermissions should appear in at least one role", () => {
      for (const perm of allPermissions) {
        const inSomeRole = Object.values(rolePermissions).some((set) =>
          set.has(perm)
        );
        expect(inSomeRole).toBe(true);
      }
    });
  });
});
