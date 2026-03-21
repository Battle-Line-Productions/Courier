using System.Security.Claims;
using Courier.Domain.Enums;
using Courier.Features.Security;
using Microsoft.AspNetCore.Authorization;
using Shouldly;

namespace Courier.Tests.Unit.Security;

public class PermissionHandlerTests
{
    private readonly PermissionHandler _handler = new();

    private async Task<bool> EvaluateAsync(string role, Permission permission)
    {
        var claims = new[] { new Claim(ClaimTypes.Role, role) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task Admin_HasAllPermissions()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            var result = await EvaluateAsync("admin", permission);
            result.ShouldBeTrue($"Admin should have {permission}");
        }
    }

    [Theory]
    [InlineData("viewer", Permission.JobsCreate, false)]
    [InlineData("viewer", Permission.JobsView, true)]
    [InlineData("operator", Permission.ConnectionsCreate, false)]
    [InlineData("operator", Permission.JobsCreate, true)]
    [InlineData("operator", Permission.PgpKeysManage, false)]
    [InlineData("operator", Permission.MonitorsCreate, true)]
    [InlineData("viewer", Permission.UsersManage, false)]
    [InlineData("admin", Permission.UsersManage, true)]
    public async Task RolePermissionCheck(string role, Permission perm, bool expected)
    {
        var result = await EvaluateAsync(role, perm);
        result.ShouldBe(expected, $"{role} + {perm} should be {expected}");
    }

    [Fact]
    public async Task NoRoleClaim_Fails()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        var requirement = new PermissionRequirement(Permission.JobsView);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleRoleClaims_ChecksAll()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "viewer"),
            new Claim(ClaimTypes.Role, "operator"),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var requirement = new PermissionRequirement(Permission.JobsCreate);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        // operator has JobsCreate, so should succeed even though viewer doesn't
        context.HasSucceeded.ShouldBeTrue();
    }
}
