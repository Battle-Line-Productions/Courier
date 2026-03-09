using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Features.Feedback;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Courier.Tests.Unit.Feedback;

public class GitHubAuthServiceTests
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly IOptions<GitHubSettings> _settings;

    public GitHubAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CourierDbContext(options);

        _encryptor = Substitute.For<ICredentialEncryptor>();
        _encryptor.Encrypt(Arg.Any<string>()).Returns(ci => Encoding.UTF8.GetBytes((string)ci[0]));
        _encryptor.Decrypt(Arg.Any<byte[]>()).Returns(ci => Encoding.UTF8.GetString((byte[])ci[0]));

        _settings = Options.Create(new GitHubSettings
        {
            OAuthClientId = "test-client-id",
            OAuthClientSecret = "test-client-secret",
            Owner = "test-owner",
            Repository = "test-repo",
        });
    }

    private GitHubAuthService CreateService(IHttpClientFactory? httpFactory = null)
    {
        httpFactory ??= CreateMockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK));
        return new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
    }

    private User CreateTestUser(
        long? gitHubId = null,
        string? gitHubUsername = null,
        byte[]? gitHubToken = null,
        DateTime? gitHubLinkedAt = null)
    {
        return new User
        {
            Id = Guid.CreateVersion7(),
            Username = "testuser",
            DisplayName = "Test User",
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            GitHubId = gitHubId,
            GitHubUsername = gitHubUsername,
            GitHubToken = gitHubToken,
            GitHubLinkedAt = gitHubLinkedAt,
        };
    }

    // --- GetOAuthUrl ---

    [Fact]
    public void GetOAuthUrl_WithValidConfig_ReturnsUrl()
    {
        var service = CreateService();

        var result = service.GetOAuthUrl("test-state");

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Url.ShouldContain("github.com/login/oauth/authorize");
        result.Data.Url.ShouldContain("client_id=test-client-id");
        result.Data.Url.ShouldContain("state=test-state");
    }

    [Fact]
    public void GetOAuthUrl_UrlContainsRepoScope()
    {
        var service = CreateService();

        var result = service.GetOAuthUrl("state");

        result.Success.ShouldBeTrue();
        result.Data!.Url.ShouldContain("scope=repo");
    }

    [Fact]
    public void GetOAuthUrl_WithEmptyClientId_ReturnsNotConfiguredError()
    {
        var settings = Options.Create(new GitHubSettings
        {
            OAuthClientId = "",
            OAuthClientSecret = "secret",
        });
        var httpFactory = CreateMockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK));
        var service = new GitHubAuthService(_db, _encryptor, httpFactory, settings);

        var result = service.GetOAuthUrl("state");

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.GitHubNotConfigured);
    }

    [Fact]
    public void GetOAuthUrl_WithNullClientId_ReturnsNotConfiguredError()
    {
        var settings = Options.Create(new GitHubSettings
        {
            OAuthClientId = null!,
        });
        var httpFactory = CreateMockHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK));
        var service = new GitHubAuthService(_db, _encryptor, httpFactory, settings);

        var result = service.GetOAuthUrl("state");

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubNotConfigured);
    }

    // --- LinkAccountAsync ---

    [Fact]
    public async Task LinkAccountAsync_WithValidCode_SavesGitHubFields()
    {
        var user = CreateTestUser();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var handler = new MockHttpHandler(request =>
        {
            if (request.RequestUri!.Host == "github.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { access_token = "gho_testtoken123" }),
                        Encoding.UTF8, "application/json")
                };
            }

            // api.github.com/user
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id = 12345L, login = "ghuser" }),
                    Encoding.UTF8, "application/json")
            };
        });

        var httpFactory = CreateRoutingHttpClientFactory(handler);

        var service = new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
        var result = await service.LinkAccountAsync(user.Id, "valid-code", CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.GitHubUsername.ShouldBe("ghuser");

        var updated = await _db.Users.FindAsync(user.Id);
        updated.ShouldNotBeNull();
        updated.GitHubId.ShouldBe(12345);
        updated.GitHubUsername.ShouldBe("ghuser");
        updated.GitHubToken.ShouldNotBeNull();
        updated.GitHubLinkedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task LinkAccountAsync_TokenExchangeFails_ReturnsOAuthFailedError()
    {
        var user = CreateTestUser();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Token exchange returns failure
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var httpFactory = CreateRoutingHttpClientFactory(handler);

        var service = new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
        var result = await service.LinkAccountAsync(user.Id, "bad-code", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubOAuthFailed);
    }

    [Fact]
    public async Task LinkAccountAsync_UserInfoFails_ReturnsOAuthFailedError()
    {
        var user = CreateTestUser();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var handler = new MockHttpHandler(request =>
        {
            // Token exchange succeeds
            if (request.RequestUri!.Host == "github.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { access_token = "gho_testtoken" }),
                        Encoding.UTF8, "application/json")
                };
            }

            // User info fails
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var httpFactory = CreateRoutingHttpClientFactory(handler);

        var service = new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
        var result = await service.LinkAccountAsync(user.Id, "valid-code", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubOAuthFailed);
    }

    [Fact]
    public async Task LinkAccountAsync_UserNotFound_ReturnsUserNotFoundError()
    {
        // Token + user info both succeed, but local user doesn't exist
        var handler = new MockHttpHandler(request =>
        {
            if (request.RequestUri!.Host == "github.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { access_token = "gho_token" }),
                        Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id = 999L, login = "ghost" }),
                    Encoding.UTF8, "application/json")
            };
        });

        var httpFactory = CreateRoutingHttpClientFactory(handler);

        var service = new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
        var result = await service.LinkAccountAsync(Guid.NewGuid(), "code", CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.UserNotFound);
    }

    [Fact]
    public async Task LinkAccountAsync_EncryptsToken()
    {
        var user = CreateTestUser();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var handler = new MockHttpHandler(request =>
        {
            if (request.RequestUri!.Host == "github.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { access_token = "gho_secret" }),
                        Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { id = 1L, login = "user" }),
                    Encoding.UTF8, "application/json")
            };
        });

        var httpFactory = CreateRoutingHttpClientFactory(handler);

        var service = new GitHubAuthService(_db, _encryptor, httpFactory, _settings);
        await service.LinkAccountAsync(user.Id, "code", CancellationToken.None);

        _encryptor.Received(1).Encrypt("gho_secret");
    }

    // --- UnlinkAccountAsync ---

    [Fact]
    public async Task UnlinkAccountAsync_ClearsGitHubFields()
    {
        var user = CreateTestUser(
            gitHubId: 12345,
            gitHubUsername: "ghuser",
            gitHubToken: Encoding.UTF8.GetBytes("encrypted-token"),
            gitHubLinkedAt: DateTime.UtcNow);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.UnlinkAccountAsync(user.Id, CancellationToken.None);

        result.Success.ShouldBeTrue();

        var updated = await _db.Users.FindAsync(user.Id);
        updated.ShouldNotBeNull();
        updated.GitHubId.ShouldBeNull();
        updated.GitHubUsername.ShouldBeNull();
        updated.GitHubToken.ShouldBeNull();
        updated.GitHubLinkedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UnlinkAccountAsync_UserNotFound_ReturnsError()
    {
        var service = CreateService();
        var result = await service.UnlinkAccountAsync(Guid.NewGuid(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.UserNotFound);
    }

    [Fact]
    public async Task UnlinkAccountAsync_AlreadyUnlinked_StillSucceeds()
    {
        var user = CreateTestUser(); // No GitHub fields set
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.UnlinkAccountAsync(user.Id, CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    // --- GetDecryptedTokenAsync ---

    [Fact]
    public async Task GetDecryptedTokenAsync_WithLinkedAccount_ReturnsDecryptedToken()
    {
        var encryptedToken = Encoding.UTF8.GetBytes("my-secret-token");
        var user = CreateTestUser(gitHubToken: encryptedToken);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var service = CreateService();
        var token = await service.GetDecryptedTokenAsync(user.Id, CancellationToken.None);

        token.ShouldBe("my-secret-token");
    }

    [Fact]
    public async Task GetDecryptedTokenAsync_WithNoToken_ReturnsNull()
    {
        var user = CreateTestUser(); // GitHubToken is null
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var service = CreateService();
        var token = await service.GetDecryptedTokenAsync(user.Id, CancellationToken.None);

        token.ShouldBeNull();
    }

    [Fact]
    public async Task GetDecryptedTokenAsync_UserNotFound_ReturnsNull()
    {
        var service = CreateService();
        var token = await service.GetDecryptedTokenAsync(Guid.NewGuid(), CancellationToken.None);

        token.ShouldBeNull();
    }

    [Fact]
    public async Task GetDecryptedTokenAsync_CallsDecryptOnEncryptor()
    {
        var encryptedToken = Encoding.UTF8.GetBytes("encrypted-value");
        var user = CreateTestUser(gitHubToken: encryptedToken);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var service = CreateService();
        await service.GetDecryptedTokenAsync(user.Id, CancellationToken.None);

        _encryptor.Received(1).Decrypt(Arg.Any<byte[]>());
    }

    // --- Helpers ---

    private static IHttpClientFactory CreateMockHttpClientFactory(HttpResponseMessage response)
    {
        var handler = new MockHttpHandler(_ => response);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        return factory;
    }

    /// <summary>
    /// Creates a factory where the "GitHub" named client gets BaseAddress set to api.github.com
    /// so relative URLs like "user" resolve correctly.
    /// </summary>
    private static IHttpClientFactory CreateRoutingHttpClientFactory(MockHttpHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(callInfo =>
        {
            var client = new HttpClient(handler);
            var name = (string)callInfo[0];
            if (name == "GitHub")
            {
                client.BaseAddress = new Uri("https://api.github.com/");
            }
            return client;
        });
        return factory;
    }

    private class MockHttpHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
