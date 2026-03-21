using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class SettingsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- GET /api/v1/settings/auth (just [Authorize], no permission gate — all authenticated roles OK) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetAuthSettings_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/settings/auth");
        AssertAuthorized(response);
    }

    // --- PUT /api/v1/settings/auth (SettingsManage — admin only) ---

    [Fact]
    public async Task UpdateAuthSettings_Admin_NotForbidden()
    {
        // First read current settings so we can restore them
        var getResponse = await Fixture.AdminClient.GetAsync("api/v1/settings/auth");
        var currentSettings = await getResponse.Content.ReadAsStringAsync();

        var response = await Fixture.AdminClient.PutAsJsonAsync("api/v1/settings/auth", new
        {
            authProvider = "local",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateAuthSettings_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PutAsJsonAsync("api/v1/settings/auth", new
        {
            authProvider = "local",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task GetAuthSettings_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/settings/auth");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
