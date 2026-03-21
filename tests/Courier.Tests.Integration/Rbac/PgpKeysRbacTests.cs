using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class PgpKeysRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    /// <summary>
    /// Asserts the request was not blocked by the authorization middleware.
    /// Some endpoints return a business-logic 403 (e.g., share links disabled)
    /// which is distinct from the framework's auth 403. The framework 403 has an
    /// empty body while the business 403 contains a JSON error response.
    /// </summary>
    private static async Task AssertNotAuthForbidden(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden)
            return; // Not forbidden at all — authorized

        // If it IS 403, check if it's a business-logic 403 (has JSON body) vs auth 403 (empty)
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrWhiteSpace(
            "Got a 403 with empty body — this is an authorization Forbidden, not a business-logic Forbidden");
    }

    // --- View Endpoints (all roles allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListPgpKeys_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/pgp-keys");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetPgpKey_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/pgp-keys/{fakeId}");
        AssertAuthorized(response);
    }

    // --- Export Public (all roles allowed — PgpKeysExportPublic) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ExportPublicKey_AllRolesAllowed(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/pgp-keys/{fakeId}/export/public");
        AssertAuthorized(response);
    }

    // --- Generate (admin only) ---

    [Fact]
    public async Task GeneratePgpKey_Admin_NotForbidden()
    {
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/pgp-keys/generate", new
        {
            name = "rbac-test-pgp-generate",
            email = "rbac@test.com",
            algorithm = "rsa",
            keySize = 2048,
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GeneratePgpKey_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/pgp-keys/generate", new
        {
            name = "rbac-test-pgp-generate-fail",
            email = "rbac@test.com",
            algorithm = "rsa",
            keySize = 2048,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Import (admin only) ---

    [Fact]
    public async Task ImportPgpKey_Admin_NotForbidden()
    {
        // Use multipart form; the endpoint expects a file upload but we just check auth
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("rbac-test-import"), "name");
        content.Add(new ByteArrayContent(new byte[] { 0 }), "file", "key.asc");

        var response = await Fixture.AdminClient.PostAsync("api/v1/pgp-keys/import", content);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ImportPgpKey_NonAdmin_Forbidden(string role)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("rbac-test-import-fail"), "name");
        content.Add(new ByteArrayContent(new byte[] { 0 }), "file", "key.asc");

        var response = await ClientForRole(role).PostAsync("api/v1/pgp-keys/import", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Fact]
    public async Task UpdatePgpKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PutAsJsonAsync($"api/v1/pgp-keys/{fakeId}", new
        {
            name = "rbac-pgp-update",
            description = "Updated by admin",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdatePgpKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/pgp-keys/{fakeId}", new
        {
            name = "rbac-pgp-update-fail",
            description = "Should be forbidden",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Fact]
    public async Task DeletePgpKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync($"api/v1/pgp-keys/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeletePgpKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/pgp-keys/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Retire (admin only) ---

    [Fact]
    public async Task RetirePgpKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/pgp-keys/{fakeId}/retire", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task RetirePgpKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/pgp-keys/{fakeId}/retire", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Revoke (admin only) ---

    [Fact]
    public async Task RevokePgpKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/pgp-keys/{fakeId}/revoke", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task RevokePgpKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/pgp-keys/{fakeId}/revoke", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Activate (admin only) ---

    [Fact]
    public async Task ActivatePgpKey_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsync(
            $"api/v1/pgp-keys/{fakeId}/activate", null);
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ActivatePgpKey_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync(
            $"api/v1/pgp-keys/{fakeId}/activate", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Set Successor (admin only) ---

    [Fact]
    public async Task SetSuccessor_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsJsonAsync(
            $"api/v1/pgp-keys/{fakeId}/set-successor", new
            {
                successorKeyId = Guid.NewGuid(),
            });
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task SetSuccessor_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsJsonAsync(
            $"api/v1/pgp-keys/{fakeId}/set-successor", new
            {
                successorKeyId = Guid.NewGuid(),
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Share Link Endpoints (admin only — PgpKeysManageSharing) ---

    [Fact]
    public async Task CreateShareLink_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsJsonAsync(
            $"api/v1/pgp-keys/{fakeId}/share", new
            {
                expiresInHours = 24,
            });
        // The service may return 403 when share links are disabled (business-logic 403,
        // not auth 403). We verify the request reached the controller by checking the
        // response body contains an error code (auth 403 has no body from the framework).
        await AssertNotAuthForbidden(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateShareLink_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsJsonAsync(
            $"api/v1/pgp-keys/{fakeId}/share", new
            {
                expiresInHours = 24,
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListShareLinks_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.GetAsync($"api/v1/pgp-keys/{fakeId}/share");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListShareLinks_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/pgp-keys/{fakeId}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteShareLink_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var fakeLinkId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync(
            $"api/v1/pgp-keys/{fakeId}/share/{fakeLinkId}");
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
            $"api/v1/pgp-keys/{fakeId}/share/{fakeLinkId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListPgpKeys_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/pgp-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
