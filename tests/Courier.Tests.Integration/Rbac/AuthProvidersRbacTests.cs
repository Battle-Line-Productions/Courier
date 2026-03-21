using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class AuthProvidersRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- Login Options ([AllowAnonymous] — all roles and anonymous allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    [InlineData("anonymous")]
    public async Task GetLoginOptions_AllRolesAndAnonymousAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth-providers/login-options");
        AssertAuthorized(response);
    }

    // --- View Endpoints (admin + operator only) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task ListAuthProviders_AdminAndOperator_Allowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth-providers");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task ListAuthProviders_Viewer_Forbidden()
    {
        var response = await Fixture.ViewerClient.GetAsync("api/v1/auth-providers");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListAuthProviders_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/auth-providers");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- Create (admin only) ---

    [Fact]
    public async Task CreateAuthProvider_Admin_NotForbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/auth-providers", new
        {
            type = "oidc",
            name = $"rbac-provider-{suffix}",
            isEnabled = false,
            configuration = new { authority = "https://test.example.com", clientId = "test", clientSecret = "test" },
            autoProvision = true,
            defaultRole = "viewer",
            allowLocalPassword = false,
            displayOrder = 0,
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/auth-providers/{id.GetString()}");
            }
        }
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateAuthProvider_NonAdmin_Forbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/auth-providers", new
        {
            type = "oidc",
            name = $"rbac-provider-{suffix}",
            isEnabled = false,
            configuration = new { authority = "https://test.example.com", clientId = "test", clientSecret = "test" },
            autoProvision = true,
            defaultRole = "viewer",
            allowLocalPassword = false,
            displayOrder = 0,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateAuthProvider_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/auth-providers/{fakeId}", new
        {
            type = "oidc",
            name = "rbac-provider-update-should-fail",
            isEnabled = false,
            configuration = new { authority = "https://test.example.com", clientId = "test", clientSecret = "test" },
            autoProvision = true,
            defaultRole = "viewer",
            allowLocalPassword = false,
            displayOrder = 0,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteAuthProvider_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/auth-providers/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Test Connection (admin only — uses AuthProvidersEdit permission) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task TestConnection_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/auth-providers/{fakeId}/test", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
