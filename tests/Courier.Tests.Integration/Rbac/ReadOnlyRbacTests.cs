using System.Net;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class ReadOnlyRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- Dashboard ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DashboardSummary_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/dashboard/summary");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DashboardRecentExecutions_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/dashboard/recent-executions");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DashboardActiveMonitors_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/dashboard/active-monitors");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DashboardKeyExpiry_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/dashboard/key-expiry");
        AssertAuthorized(response);
    }

    // --- Audit Log ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task AuditLog_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/audit-log");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task AuditLogByEntity_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/audit-log/entity/job/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Notification Logs ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task NotificationLogs_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/notification-logs");
        AssertAuthorized(response);
    }

    // --- Filesystem Browse ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task FilesystemBrowse_AllRolesAllowed(string role)
    {
        // May return error for missing path param, but should NOT be 403
        var response = await ClientForRole(role).GetAsync("api/v1/filesystem/browse?path=/tmp");
        AssertAuthorized(response);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task Dashboard_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/dashboard/summary");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuditLog_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/audit-log");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NotificationLogs_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/notification-logs");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FilesystemBrowse_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/filesystem/browse?path=/tmp");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
