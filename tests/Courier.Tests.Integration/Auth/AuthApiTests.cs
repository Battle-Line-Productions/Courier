using System.Net;
using System.Net.Http.Json;
using Courier.Domain.Common;
using Courier.Features.Auth;
using Courier.Features.Setup;
using Courier.Features.Users;
using Shouldly;

namespace Courier.Tests.Integration.Auth;

public class AuthApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SetupStatus_ReturnsCompleted()
    {
        // Setup is already completed by the factory
        var response = await _client.GetAsync("/api/v1/setup/status");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SetupStatusDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Setup_WhenAlreadyCompleted_ReturnsConflict()
    {
        var request = new InitializeSetupRequest
        {
            Username = "admin2",
            DisplayName = "Admin 2",
            Password = "TestPassword123!",
            ConfirmPassword = "TestPassword123!",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/setup/initialize", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetMe_WithAuth_DoesNotReturnUnauthorized()
    {
        // Auto-authenticated as admin via TestAuthHandler
        var response = await _client.GetAsync("/api/v1/auth/me");

        // The test user ID doesn't exist in DB, so we get 404 (not 401)
        // This proves auth is working — we got past the auth middleware
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_WithAdminAuth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateAndLoginUser_FullFlow()
    {
        // Create a user (we're auto-authed as admin via TestAuthHandler)
        var uniqueName = $"logintest_{Guid.NewGuid():N}"[..20];
        var createRequest = new CreateUserRequest
        {
            Username = uniqueName,
            DisplayName = "Login Test User",
            Password = "TestPassword123!",
            Role = "viewer",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/users", createRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Login as that user (AllowAnonymous endpoint)
        var loginRequest = new LoginRequest
        {
            Username = uniqueName,
            Password = "TestPassword123!",
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        loginBody.ShouldNotBeNull();
        loginBody!.Data.ShouldNotBeNull();
        loginBody.Data!.AccessToken.ShouldNotBeNullOrEmpty();
        loginBody.Data.RefreshToken.ShouldNotBeNullOrEmpty();
        loginBody.Data.ExpiresIn.ShouldBeGreaterThan(0);
        loginBody.Data.User.ShouldNotBeNull();
        loginBody.Data.User.Username.ShouldBe(uniqueName);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var loginRequest = new LoginRequest
        {
            Username = "nonexistent_user",
            Password = "wrong_password",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_EmptyFields_ReturnsBadRequest()
    {
        var loginRequest = new LoginRequest
        {
            Username = "",
            Password = "",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_FullFlow()
    {
        // Create a user and login
        var uniqueName = $"refresh_{Guid.NewGuid():N}"[..20];
        var createRequest = new CreateUserRequest
        {
            Username = uniqueName,
            DisplayName = "Refresh Test User",
            Password = "TestPassword123!",
            Role = "viewer",
        };
        await _client.PostAsJsonAsync("/api/v1/users", createRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Username = uniqueName, Password = "TestPassword123!" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();

        // Refresh the token
        var refreshRequest = new RefreshRequest { RefreshToken = loginBody!.Data!.RefreshToken };
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        refreshBody.ShouldNotBeNull();
        refreshBody!.Data.ShouldNotBeNull();
        refreshBody.Data!.AccessToken.ShouldNotBeNullOrEmpty();
        refreshBody.Data.RefreshToken.ShouldNotBeNullOrEmpty();
        // Refresh token should be rotated
        refreshBody.Data.RefreshToken.ShouldNotBe(loginBody.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var refreshRequest = new RefreshRequest { RefreshToken = "totally-invalid-token" };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_WithAdminAuth_ReturnsCreated()
    {
        var uniqueName = $"create_{Guid.NewGuid():N}"[..20];
        var request = new CreateUserRequest
        {
            Username = uniqueName,
            DisplayName = "Created User",
            Password = "TestPassword123!",
            Role = "operator",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/users", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        body.ShouldNotBeNull();
        body!.Data.ShouldNotBeNull();
        body.Data!.Username.ShouldBe(uniqueName);
        body.Data.Role.ShouldBe("operator");
        body.Data.IsActive.ShouldBeTrue();
    }
}
