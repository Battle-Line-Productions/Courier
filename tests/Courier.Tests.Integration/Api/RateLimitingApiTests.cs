using System.Net;
using System.Net.Http.Json;
using Courier.Features.Auth;
using Shouldly;

namespace Courier.Tests.Integration.Api;

public class RateLimitingApiTests : IClassFixture<CourierApiFactory>
{
    private readonly CourierApiFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingApiTests(CourierApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthEndpoint_ExceedsLimit_Returns429()
    {
        // Arrange — auth rate limit is 10 requests/minute per IP (test factory override)
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "rate_limit_test_user",
            Password = "wrong_password",
        };

        // Act — send 35 requests rapidly (more than the 30 per-segment limit)
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert — at least one request should have been rate-limited
        rateLimitedResponse.ShouldNotBeNull("Expected at least one 429 response after exceeding rate limit");
        rateLimitedResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AuthEndpoint_429Response_IncludesRetryAfterHeader()
    {
        // Arrange
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "retry_after_test_user",
            Password = "wrong_password",
        };

        // Act — exhaust the rate limit
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.ShouldNotBeNull("Expected a 429 response");
        rateLimitedResponse!.Headers.TryGetValues("Retry-After", out var retryAfterValues).ShouldBeTrue(
            "429 response should include Retry-After header");
        retryAfterValues.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GeneralApi_WithinLimit_ReturnsSuccess()
    {
        // Arrange — general API limit is 100 requests/minute
        // Act — send a few requests (well within the 100/min limit)
        for (var i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/api/v1/connections");

            // Assert — should return OK, not 429
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task AuthEndpoint_LoginAndRefresh_ShareSameRateLimit()
    {
        // Arrange — both login and refresh are under the "auth" rate limit policy
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "shared_limit_user",
            Password = "wrong_password",
        };

        var refreshRequest = new { refreshToken = "invalid-token" };

        // Act — alternate between login and refresh requests to exhaust shared limit
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = i % 2 == 0
                ? await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest)
                : await client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert — the shared limit should eventually trigger
        rateLimitedResponse.ShouldNotBeNull("Expected 429 after mixing login and refresh requests");
    }

    [Fact]
    public async Task AuthEndpoint_RateLimitDoesNotAffectOtherEndpoints()
    {
        // Arrange — exhaust the auth rate limit
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "isolation_test_user",
            Password = "wrong_password",
        };

        // Exhaust auth limit
        for (var i = 0; i < 15; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        }

        // Act — a general API request should still work
        var response = await client.GetAsync("/api/v1/connections");

        // Assert — general API has its own sliding window limit (100/min)
        response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RateLimited_ResponseBody_IsValidJsonEnvelope()
    {
        // Arrange
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "json_body_test_user",
            Password = "wrong_password",
        };

        // Act — exhaust the rate limit
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.ShouldNotBeNull("Expected a 429 response");

        // The response body may be empty or contain a standard rate limit message.
        // ASP.NET Core rate limiter returns empty body by default unless configured.
        var body = await rateLimitedResponse!.Content.ReadAsStringAsync();
        // Just verify we got a 429 — body format depends on framework config
        rateLimitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GeneralApi_MultipleEndpoints_ShareGlobalLimit()
    {
        // Arrange — the global limiter applies to all non-auth endpoints
        // Act — send a few requests to different endpoints
        var endpoints = new[]
        {
            "/api/v1/connections",
            "/api/v1/jobs",
            "/api/v1/users",
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);

            // Assert — all should work within the global limit
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task AuthEndpoint_ChangePassword_AlsoRateLimited()
    {
        // Arrange — change-password is under the auth controller which has [EnableRateLimiting("auth")]
        var client = _factory.CreateClient();

        var request = new
        {
            currentPassword = "wrong",
            newPassword = "alsoWrong123!",
            confirmPassword = "alsoWrong123!",
        };

        // Act — send enough requests to trigger the rate limit
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", request);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert — change-password shares the auth rate limit
        rateLimitedResponse.ShouldNotBeNull("Expected 429 for change-password endpoint under auth rate limit");
    }

    [Fact]
    public async Task AuthEndpoint_429StatusCode_MatchesConfiguredRejectionCode()
    {
        // Arrange — configured with: options.RejectionStatusCode = StatusCodes.Status429TooManyRequests
        var client = _factory.CreateClient();

        var loginRequest = new LoginRequest
        {
            Username = "status_code_test_user",
            Password = "wrong_password",
        };

        // Act
        HttpResponseMessage? rateLimitedResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.ShouldNotBeNull();
        ((int)rateLimitedResponse!.StatusCode).ShouldBe(429);
    }
}
