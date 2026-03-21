using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class AuthRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- GET /api/v1/auth/me ([Authorize] — all authenticated roles) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task Me_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth/me");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task Me_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/auth/me");
        // The auth controller has [EnableRateLimiting("auth")] at class level,
        // so anonymous requests may get 429 (TooManyRequests) instead of 401.
        // Both indicate the request was rejected — neither is 200/Forbidden.
        var status = response.StatusCode;
        (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.TooManyRequests)
            .ShouldBeTrue($"Expected Unauthorized or TooManyRequests but got {status}");
    }

    // --- POST /api/v1/auth/login ([AllowAnonymous] — proves anonymous is not blocked) ---

    [Fact]
    public async Task Login_Anonymous_NotForbidden()
    {
        // Login with invalid credentials — we just verify it's not 403 (Forbidden).
        // The auth controller has [EnableRateLimiting("auth")] which may return 429,
        // and AllowAnonymous may still get 401 from the test auth handler's challenge.
        // The key RBAC assertion is that it's not 403.
        var response = await Fixture.AnonymousClient.PostAsJsonAsync("api/v1/auth/login", new
        {
            username = "nonexistent",
            password = "irrelevant",
        });

        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    // --- POST /api/v1/auth/refresh ([AllowAnonymous]) ---

    [Fact]
    public async Task Refresh_Anonymous_NotForbidden()
    {
        var response = await Fixture.AnonymousClient.PostAsJsonAsync("api/v1/auth/refresh", new
        {
            refreshToken = "invalid-token",
        });

        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }

    // --- POST /api/v1/auth/logout ([Authorize]) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task Logout_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/auth/logout", new
        {
            refreshToken = "some-token",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task Logout_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.PostAsJsonAsync("api/v1/auth/logout", new
        {
            refreshToken = "some-token",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- POST /api/v1/auth/change-password ([Authorize]) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ChangePassword_AllRolesAllowed(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/auth/change-password", new
        {
            currentPassword = "old-password",
            newPassword = "N3wStr0ng!Pass#789",
        });

        AssertAuthorized(response);
    }

    [Fact]
    public async Task ChangePassword_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.PostAsJsonAsync("api/v1/auth/change-password", new
        {
            currentPassword = "old-password",
            newPassword = "N3wStr0ng!Pass#789",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
