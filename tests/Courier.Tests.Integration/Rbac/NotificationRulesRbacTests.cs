using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class NotificationRulesRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
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
    public async Task ListNotificationRules_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/notification-rules");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetNotificationRule_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/notification-rules/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Create (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task CreateNotificationRule_AllowedRoles_NotForbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/notification-rules", new
        {
            name = $"rbac-rule-{role}-{suffix}",
            eventType = "job_failed",
            channel = "email",
            destination = "test@example.com",
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/notification-rules/{id.GetString()}");
            }
        }
    }

    [Fact]
    public async Task CreateNotificationRule_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/notification-rules", new
        {
            name = "rbac-rule-viewer-should-fail",
            eventType = "job_failed",
            channel = "email",
            destination = "test@example.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UpdateNotificationRule_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/notification-rules/{fakeId}", new
        {
            name = "rbac-rule-update",
            eventType = "job_failed",
            channel = "email",
            destination = "test@example.com",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UpdateNotificationRule_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PutAsJsonAsync($"api/v1/notification-rules/{fakeId}", new
        {
            name = "rbac-rule-update-fail",
            eventType = "job_failed",
            channel = "email",
            destination = "test@example.com",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DeleteNotificationRule_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/notification-rules/{fakeId}");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task DeleteNotificationRule_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").DeleteAsync($"api/v1/notification-rules/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Test (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task TestNotificationRule_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/notification-rules/{fakeId}/test", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task TestNotificationRule_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/notification-rules/{fakeId}/test", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListNotificationRules_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/notification-rules");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
