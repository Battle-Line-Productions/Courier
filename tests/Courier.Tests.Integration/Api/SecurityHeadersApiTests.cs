using System.Net;
using Shouldly;

namespace Courier.Tests.Integration.Api;

public class SecurityHeadersApiTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersApiTests(CourierApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_ContainsXContentTypeOptionsNosniff()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("X-Content-Type-Options", out var values).ShouldBeTrue();
        values.ShouldContain("nosniff");
    }

    [Fact]
    public async Task Response_ContainsXFrameOptionsDeny()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("X-Frame-Options", out var values).ShouldBeTrue();
        values.ShouldContain("DENY");
    }

    [Fact]
    public async Task Response_ContainsContentSecurityPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("Content-Security-Policy", out var values).ShouldBeTrue();
        var csp = string.Join(", ", values!);
        csp.ShouldContain("default-src");
    }

    [Fact]
    public async Task Response_ContainsReferrerPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("Referrer-Policy", out var values).ShouldBeTrue();
        values.ShouldContain("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Response_ContainsXXssProtectionZero()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("X-XSS-Protection", out var values).ShouldBeTrue();
        values.ShouldContain("0");
    }

    [Fact]
    public async Task Response_ContainsPermissionsPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/connections");

        // Assert
        response.Headers.TryGetValues("Permissions-Policy", out var values).ShouldBeTrue();
        var pp = string.Join(", ", values!);
        pp.ShouldContain("camera=()");
        pp.ShouldContain("microphone=()");
        pp.ShouldContain("geolocation=()");
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnDifferentEndpoints()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/v1/connections",
            "/api/v1/jobs",
            "/api/v1/users",
        };

        foreach (var endpoint in endpoints)
        {
            // Act
            var response = await _client.GetAsync(endpoint);

            // Assert
            response.Headers.TryGetValues("X-Content-Type-Options", out var xContentType).ShouldBeTrue(
                $"Missing X-Content-Type-Options on {endpoint}");
            xContentType.ShouldContain("nosniff");

            response.Headers.TryGetValues("X-Frame-Options", out var xFrame).ShouldBeTrue(
                $"Missing X-Frame-Options on {endpoint}");
            xFrame.ShouldContain("DENY");

            response.Headers.TryGetValues("Content-Security-Policy", out var csp).ShouldBeTrue(
                $"Missing Content-Security-Policy on {endpoint}");
            csp.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnNotFoundResponse()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"/api/v1/connections/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        response.Headers.TryGetValues("X-Content-Type-Options", out var xContentType).ShouldBeTrue();
        xContentType.ShouldContain("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var xFrame).ShouldBeTrue();
        xFrame.ShouldContain("DENY");

        response.Headers.TryGetValues("Content-Security-Policy", out var csp).ShouldBeTrue();
        csp.ShouldNotBeEmpty();

        response.Headers.TryGetValues("Referrer-Policy", out var referrer).ShouldBeTrue();
        referrer.ShouldContain("strict-origin-when-cross-origin");

        response.Headers.TryGetValues("X-XSS-Protection", out var xss).ShouldBeTrue();
        xss.ShouldContain("0");

        response.Headers.TryGetValues("Permissions-Policy", out var pp).ShouldBeTrue();
        pp.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SecurityHeaders_PresentOnAuthEndpoint()
    {
        // Arrange & Act — auth/me is an authenticated endpoint
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert — regardless of the response status, security headers should be present
        response.Headers.TryGetValues("X-Content-Type-Options", out var xContentType).ShouldBeTrue();
        xContentType.ShouldContain("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var xFrame).ShouldBeTrue();
        xFrame.ShouldContain("DENY");
    }
}
