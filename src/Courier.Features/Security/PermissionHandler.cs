using System.Security.Claims;
using Courier.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Evaluates PermissionRequirement by checking if any of the user's role claims
/// have the required permission. Stateless — registered as Singleton.
/// If dynamic permission lookup is ever needed (e.g., DB-backed),
/// change registration to Scoped to avoid captive dependency bugs.
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);

        if (roles.Any(role => RolePermissions.HasPermission(role, requirement.Permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
