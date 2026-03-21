using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class MonitorsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- View Endpoints (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListMonitors_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/monitors");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetMonitor_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/monitors/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetMonitorFileLog_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/monitors/{fakeId}/file-log");
        AssertAuthorized(response);
    }

    // --- Create (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task CreateMonitor_AllowedRoles_NotForbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/monitors", new
        {
            name = $"rbac-monitor-{role}-{suffix}",
            connectionId = Guid.NewGuid(),
            remotePath = "/tmp/test",
            pattern = "*.csv",
            scheduleExpression = "0 * * * *",
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/monitors/{id.GetString()}");
            }
        }
    }

    [Fact]
    public async Task CreateMonitor_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/monitors", new
        {
            name = "rbac-monitor-viewer-should-fail",
            connectionId = Guid.NewGuid(),
            remotePath = "/tmp/test",
            pattern = "*.csv",
            scheduleExpression = "0 * * * *",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UpdateMonitor_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/monitors/{fakeId}", new
        {
            name = "rbac-monitor-update",
            connectionId = Guid.NewGuid(),
            remotePath = "/tmp/test",
            pattern = "*.csv",
            scheduleExpression = "0 * * * *",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UpdateMonitor_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PutAsJsonAsync($"api/v1/monitors/{fakeId}", new
        {
            name = "rbac-monitor-update-fail",
            connectionId = Guid.NewGuid(),
            remotePath = "/tmp/test",
            pattern = "*.csv",
            scheduleExpression = "0 * * * *",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DeleteMonitor_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/monitors/{fakeId}");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task DeleteMonitor_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").DeleteAsync($"api/v1/monitors/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- State Changes (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task ActivateMonitor_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/monitors/{fakeId}/activate", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task ActivateMonitor_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/monitors/{fakeId}/activate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task PauseMonitor_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/monitors/{fakeId}/pause", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task PauseMonitor_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/monitors/{fakeId}/pause", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DisableMonitor_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/monitors/{fakeId}/disable", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task DisableMonitor_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/monitors/{fakeId}/disable", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task AcknowledgeError_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/monitors/{fakeId}/acknowledge-error", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task AcknowledgeError_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/monitors/{fakeId}/acknowledge-error", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListMonitors_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/monitors");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
