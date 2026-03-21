using Courier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Convenience attribute for permission-based authorization.
/// Usage: [RequirePermission(Permission.JobsCreate)]
/// Resolves to a named policy matching the Permission enum name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(Permission permission)
        : base(permission.ToString()) { }
}
