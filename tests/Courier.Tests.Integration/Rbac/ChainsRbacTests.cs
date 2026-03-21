using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class ChainsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
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
    public async Task ListChains_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/chains");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetChain_AllRolesAllowed(string role)
    {
        // Use a non-existent ID — we just care about 403 vs. not
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/chains/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Create (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task CreateChain_AllowedRoles_NotForbidden(string role)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/chains", new
        {
            name = $"rbac-chain-{role}-{suffix}",
            description = "RBAC test chain",
        });

        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/chains/{id.GetString()}");
            }
        }
    }

    [Fact]
    public async Task CreateChain_Viewer_Forbidden()
    {
        var response = await ClientForRole("viewer").PostAsJsonAsync("api/v1/chains", new
        {
            name = "rbac-chain-viewer-should-fail",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task UpdateChain_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/chains/{fakeId}", new
        {
            name = "rbac-chain-update-test",
            description = $"Updated by {role}",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task UpdateChain_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PutAsJsonAsync($"api/v1/chains/{fakeId}", new
        {
            name = "rbac-chain-update-test",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task DeleteChain_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/chains/{fakeId}");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task DeleteChain_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").DeleteAsync($"api/v1/chains/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Execute (admin + operator allowed, viewer forbidden) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task ExecuteChain_AllowedRoles_NotForbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/chains/{fakeId}/execute", null);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task ExecuteChain_Viewer_Forbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole("viewer").PostAsync(
            $"api/v1/chains/{fakeId}/execute", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- View sub-resources (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetChainExecutions_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/chains/{fakeId}/executions");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetChainSchedules_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/chains/{fakeId}/schedules");
        AssertAuthorized(response);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListChains_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/chains");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
