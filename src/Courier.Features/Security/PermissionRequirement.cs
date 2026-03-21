using Courier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Authorization requirement that wraps a Permission enum value.
/// Resolved by PermissionHandler.
/// </summary>
public class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;
}
