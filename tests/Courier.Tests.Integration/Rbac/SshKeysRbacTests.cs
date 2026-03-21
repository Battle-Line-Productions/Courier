using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class SshKeysRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    /// <summary>
    /// Asserts the request was not blocked by the authorization middleware.
    /// Some endpoints return a business-logic 403 (e.g., share links disabled)
    /// which is distinct from the framework's auth 403.
    /// </summary>
    private static async Task AssertNotAuthForbidden(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden)
            return;

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace(
            "Got a 403 with empty body — this is an authorization Forbidden, not a business-logic Forbidden");
    }

    // --- View Endpoints (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListSshKeys_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/ssh-keys");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetSshKey_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/ssh-keys/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Export Public (all roles allowed — SshKeysExportPublic) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ExportPublicKey_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/ssh-keys/{fakeId}/export/public");
        AssertAuthorized(response);
    }

    // --- Generate (admin only) ---

    [Fact]
    public async Task GenerateSshKey_Admin_NotForbidden()
    {
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/ssh-keys/generate", new
        {
            name = "rbac-test-ssh-generate",
            algorithm = "ed25519",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GenerateSshKey_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/ssh-keys/generate", new
        {
            name = "rbac-test-ssh-generate-fail",
            algorithm = "ed25519",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Import (admin only) ---

    [Fact]
    public async Task ImportSshKey_Admin_NotForbidden()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("rbac-test-ssh-import"), "name");
        content.Add(new ByteArrayContent(new byte[] { 0 }), "file", "key.pub");

        var response = await Fixture.AdminClient.PostAsync("api/v1/ssh-keys/import", content);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ImportSshKey_NonAdmin_Forbidden(string role)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("rbac-test-ssh-import-fail"), "name");
        content.Add(new ByteArrayContent(new byte[] { 0 }), "file", "key.pub");

        var response = await ClientForRole(role).PostAsync("api/v1/ssh-keys/import", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Fact]
    public async Task UpdateSshKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PutAsJsonAsync($"api/v1/ssh-keys/{fakeId}", new
        {
            name = "rbac-ssh-update",
            description = "Updated by admin",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateSshKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/ssh-keys/{fakeId}", new
        {
            name = "rbac-ssh-update-fail",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Fact]
    public async Task DeleteSshKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync($"api/v1/ssh-keys/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteSshKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/ssh-keys/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Retire (admin only) ---

    [Fact]
    public async Task RetireSshKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/ssh-keys/{fakeId}/retire", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task RetireSshKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/ssh-keys/{fakeId}/retire", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Activate (admin only) ---

    [Fact]
    public async Task ActivateSshKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/ssh-keys/{fakeId}/activate", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ActivateSshKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/ssh-keys/{fakeId}/activate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Share Link Endpoints (admin only — SshKeysManageSharing) ---

    [Fact]
    public async Task CreateShareLink_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsJsonAsync(
            $"api/v1/ssh-keys/{fakeId}/share", new
            {
                expiresInHours = 24,
            });
        // The service may return 403 when share links are disabled (business-logic 403).
        await AssertNotAuthForbidden(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateShareLink_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsJsonAsync(
            $"api/v1/ssh-keys/{fakeId}/share", new
            {
                expiresInHours = 24,
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListShareLinks_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.GetAsync($"api/v1/ssh-keys/{fakeId}/share");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListShareLinks_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/ssh-keys/{fakeId}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteShareLink_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var fakeLinkId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync(
            $"api/v1/ssh-keys/{fakeId}/share/{fakeLinkId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteShareLink_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var fakeLinkId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync(
            $"api/v1/ssh-keys/{fakeId}/share/{fakeLinkId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListSshKeys_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/ssh-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
