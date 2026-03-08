using System.Net;
using System.Net.Http.Json;
using Courier.Features.Auth;
using Shouldly;

namespace Courier.Tests.Integration.Api;

/// <summary>
/// Tests that verify requests within the rate limit are NOT blocked.
/// Uses its own factory instance so rate limiter state is isolated from exhaustion tests.
/// </summary>
public class RateLimitingBasicTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitingBasicTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthEndpoint_WithinLimit_ReturnsNon429()
    {
        // Arrange — auth rate limit is 10 requests/minute per IP
        var loginRequest = new LoginRequest
        {
            Username = "nonexistent_ratelimit_test",
            Password = "wrong_password",
        };

        // Act — send a single request (well within the 10/min limit)
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert — should get Unauthorized (invalid creds), NOT 429
        response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }
}
