using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class UsersRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- View Endpoints (admin only) ---

    [Fact]
    public async Task ListUsers_Admin_NotForbidden()
    {
        var response = await Fixture.AdminClient.GetAsync("api/v1/users");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ListUsers_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.GetAsync($"api/v1/users/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task GetUser_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).GetAsync($"api/v1/users/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Create (admin only) ---

    [Fact]
    public async Task CreateUser_Admin_NotForbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/users", new
        {
            username = $"rbac-user-{suffix}",
            email = $"rbac-{suffix}@test.com",
            password = "Str0ng!Pass#123",
            role = "viewer",
            displayName = "RBAC Test User",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateUser_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/users", new
        {
            username = "rbac-user-should-fail",
            email = "fail@test.com",
            password = "Str0ng!Pass#123",
            role = "viewer",
            displayName = "Should Fail",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Fact]
    public async Task UpdateUser_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PutAsJsonAsync($"api/v1/users/{fakeId}", new
        {
            email = "updated@test.com",
            role = "viewer",
            displayName = "Updated User",
        });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateUser_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/users/{fakeId}", new
        {
            email = "updated@test.com",
            role = "viewer",
            displayName = "Updated User",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Fact]
    public async Task DeleteUser_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.DeleteAsync($"api/v1/users/{fakeId}");
        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteUser_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/users/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Reset Password (admin only) ---

    [Fact]
    public async Task ResetPassword_Admin_NotForbidden()
    {
        var fakeId = Guid.NewGuid();
        var response = await Fixture.AdminClient.PostAsJsonAsync(
            $"api/v1/users/{fakeId}/reset-password", new
            {
                newPassword = "N3wStr0ng!Pass#456",
            });

        AssertAuthorized(response);
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task ResetPassword_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsJsonAsync(
            $"api/v1/users/{fakeId}/reset-password", new
            {
                newPassword = "N3wStr0ng!Pass#456",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Anonymous gets Unauthorized ---

    [Fact]
    public async Task ListUsers_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
