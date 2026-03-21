using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class ConnectionsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
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
    public async Task ListConnections_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/connections");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetConnection_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/connections/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Create (admin only) ---

    [Fact]
    public async Task CreateConnection_Admin_NotForbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/connections", new
        {
            name = $"rbac-conn-admin-{suffix}",
            protocol = "sftp",
            hostname = "test.example.com",
            port = 22,
            authMethod = "password",
            username = "testuser",
            password = "testpass",
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/connections/{id.GetString()}");
            }
        }
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateConnection_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/connections", new
        {
            name = "rbac-conn-should-fail",
            protocol = "sftp",
            hostname = "test.example.com",
            port = 22,
            authMethod = "password",
            username = "testuser",
            password = "testpass",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Fact]
    public async Task UpdateConnection_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PutAsJsonAsync($"api/v1/connections/{fakeId}", new
        {
            name = "rbac-conn-update-admin",
            protocol = "sftp",
            hostname = "test.example.com",
            port = 22,
            authMethod = "password",
            username = "testuser",
            password = "testpass",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateConnection_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/connections/{fakeId}", new
        {
            name = "rbac-conn-update-should-fail",
            protocol = "sftp",
            hostname = "test.example.com",
            port = 22,
            authMethod = "password",
            username = "testuser",
            password = "testpass",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Fact]
    public async Task DeleteConnection_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync($"api/v1/connections/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteConnection_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/connections/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Test Connection (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task TestConnection_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/connections/{fakeId}/test", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task TestConnection_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/connections/{fakeId}/test", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListConnections_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/connections");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
