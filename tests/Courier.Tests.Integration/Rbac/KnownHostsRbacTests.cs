using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class KnownHostsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
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
    public async Task ListKnownHosts_AllRolesAllowed(string role)
    {
        // Use a non-existent connection ID; we just care about 403 vs. not
        var fakeConnectionId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync(
            $"api/v1/connections/{fakeConnectionId}/known-hosts");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetKnownHost_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/known-hosts/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Create (admin only) ---

    [Fact]
    public async Task CreateKnownHost_Admin_NotForbidden()
    {
        var fakeConnectionId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsJsonAsync(
            $"api/v1/connections/{fakeConnectionId}/known-hosts", new
            {
                hostname = "test.example.com",
                fingerprint = "SHA256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=",
                keyType = "ssh-rsa",
            });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateKnownHost_NonAdmin_Forbidden(string role)
    {
        var fakeConnectionId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsJsonAsync(
            $"api/v1/connections/{fakeConnectionId}/known-hosts", new
            {
                hostname = "test.example.com",
                fingerprint = "SHA256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=",
                keyType = "ssh-rsa",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Fact]
    public async Task DeleteKnownHost_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync($"api/v1/known-hosts/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteKnownHost_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/known-hosts/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Approve (admin only) ---

    [Fact]
    public async Task ApproveKnownHost_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/known-hosts/{fakeId}/approve", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ApproveKnownHost_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/known-hosts/{fakeId}/approve", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListKnownHosts_Anonymous_Unauthorized()
    {
        var fakeConnectionId = Guid.NewGuid();
        var response = await Fixture.AnonymousClient.GetAsync(
            $"api/v1/connections/{fakeConnectionId}/known-hosts");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
