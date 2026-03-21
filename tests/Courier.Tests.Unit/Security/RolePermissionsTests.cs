using Courier.Domain.Authorization;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Security;

public class RolePermissionsTests
{
    [Fact]
    public void Admin_HasEveryPermission()
    {
        var allPermissions = Enum.GetValues<Permission>().ToHashSet();
        var adminPermissions = RolePermissions.GetPermissions("admin");

        adminPermissions.Count.ShouldBe(allPermissions.Count,
            "Admin should have every permission defined in the enum");

        foreach (var permission in allPermissions)
        {
            adminPermissions.ShouldContain(permission,
                $"Admin is missing permission: {permission}");
        }
    }

    [Fact]
    public void Operator_CanManageJobs()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.JobsView);
        op.ShouldContain(Permission.JobsCreate);
        op.ShouldContain(Permission.JobsEdit);
        op.ShouldContain(Permission.JobsDelete);
        op.ShouldContain(Permission.JobsExecute);
        op.ShouldContain(Permission.JobsManageSchedules);
        op.ShouldContain(Permission.JobsManageDependencies);
    }

    [Fact]
    public void Operator_CannotManageConnections()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.ConnectionsView);
        op.ShouldContain(Permission.ConnectionsTest);
        op.ShouldNotContain(Permission.ConnectionsCreate);
        op.ShouldNotContain(Permission.ConnectionsEdit);
        op.ShouldNotContain(Permission.ConnectionsDelete);
    }

    [Fact]
    public void Operator_CannotManageKeys()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.PgpKeysView);
        op.ShouldContain(Permission.PgpKeysExportPublic);
        op.ShouldNotContain(Permission.PgpKeysManage);
        op.ShouldNotContain(Permission.PgpKeysManageSharing);
        op.ShouldContain(Permission.SshKeysView);
        op.ShouldContain(Permission.SshKeysExportPublic);
        op.ShouldNotContain(Permission.SshKeysManage);
        op.ShouldNotContain(Permission.SshKeysManageSharing);
    }

    [Fact]
    public void Operator_CannotManageUsers()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldNotContain(Permission.UsersView);
        op.ShouldNotContain(Permission.UsersManage);
    }

    [Fact]
    public void Operator_CannotManageSettings()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.SettingsView);
        op.ShouldNotContain(Permission.SettingsManage);
    }

    [Fact]
    public void Viewer_HasOnlyViewPermissions()
    {
        var viewer = RolePermissions.GetPermissions("viewer");

        viewer.ShouldNotContain(Permission.JobsCreate);
        viewer.ShouldNotContain(Permission.JobsEdit);
        viewer.ShouldNotContain(Permission.JobsDelete);
        viewer.ShouldNotContain(Permission.JobsExecute);
        viewer.ShouldNotContain(Permission.ChainsCreate);
        viewer.ShouldNotContain(Permission.ConnectionsCreate);
        viewer.ShouldNotContain(Permission.PgpKeysManage);
        viewer.ShouldNotContain(Permission.SshKeysManage);
        viewer.ShouldNotContain(Permission.MonitorsCreate);
        viewer.ShouldNotContain(Permission.TagsManage);
        viewer.ShouldNotContain(Permission.NotificationRulesManage);
        viewer.ShouldNotContain(Permission.UsersManage);
        viewer.ShouldNotContain(Permission.SettingsManage);
        viewer.ShouldNotContain(Permission.KnownHostsManage);

        viewer.ShouldContain(Permission.JobsView);
        viewer.ShouldContain(Permission.ChainsView);
        viewer.ShouldContain(Permission.ConnectionsView);
        viewer.ShouldContain(Permission.PgpKeysView);
        viewer.ShouldContain(Permission.PgpKeysExportPublic);
        viewer.ShouldContain(Permission.SshKeysView);
        viewer.ShouldContain(Permission.SshKeysExportPublic);
        viewer.ShouldContain(Permission.MonitorsView);
        viewer.ShouldContain(Permission.TagsView);
        viewer.ShouldContain(Permission.AuditLogView);
        viewer.ShouldContain(Permission.DashboardView);
        viewer.ShouldContain(Permission.SettingsView);
        viewer.ShouldContain(Permission.FilesystemBrowse);
        viewer.ShouldContain(Permission.KnownHostsView);
    }

    [Fact]
    public void UnknownRole_HasNoPermissions()
    {
        RolePermissions.GetPermissions("hacker").ShouldBeEmpty();
        RolePermissions.GetPermissions("").ShouldBeEmpty();
        RolePermissions.GetPermissions("superadmin").ShouldBeEmpty();
    }

    [Fact]
    public void HasPermission_IsCaseInsensitive()
    {
        RolePermissions.HasPermission("Admin", Permission.JobsView).ShouldBeTrue();
        RolePermissions.HasPermission("ADMIN", Permission.JobsView).ShouldBeTrue();
        RolePermissions.HasPermission("admin", Permission.JobsView).ShouldBeTrue();
    }

    [Fact]
    public void HasPermission_ReturnsFalseForDenied()
    {
        RolePermissions.HasPermission("viewer", Permission.JobsCreate).ShouldBeFalse();
        RolePermissions.HasPermission("operator", Permission.ConnectionsCreate).ShouldBeFalse();
    }
}
