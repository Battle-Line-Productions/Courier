using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Features.Feedback;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Courier.Tests.Unit.Feedback;

public class FeedbackServiceTests
{
    private readonly CourierDbContext _db;
    private readonly GitHubAuthService _authService;
    private readonly IMemoryCache _cache;
    private readonly IOptions<GitHubSettings> _settings;

    public FeedbackServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CourierDbContext(dbOptions);

        var encryptor = Substitute.For<ICredentialEncryptor>();
        encryptor.Encrypt(Arg.Any<string>()).Returns(ci => Encoding.UTF8.GetBytes((string)ci[0]));
        encryptor.Decrypt(Arg.Any<byte[]>()).Returns(ci => Encoding.UTF8.GetString((byte[])ci[0]));

        var authHttpFactory = Substitute.For<IHttpClientFactory>();
        authHttpFactory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var authSettings = Options.Create(new GitHubSettings
        {
            OAuthClientId = "test-client-id",
            OAuthClientSecret = "test-client-secret",
            Owner = "test-owner",
            Repository = "test-repo",
        });

        _authService = new GitHubAuthService(_db, encryptor, authHttpFactory, authSettings);

        _cache = new MemoryCache(new MemoryCacheOptions());
        _settings = Options.Create(new GitHubSettings
        {
            PersonalAccessToken = "ghp_test_pat",
            Owner = "test-owner",
            Repository = "test-repo",
            CacheMinutes = 5,
        });
    }

    private FeedbackService CreateService(MockHttpHandler? handler = null, IOptions<GitHubSettings>? settingsOverride = null)
    {
        handler ??= new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });

        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com")
        });

        return new FeedbackService(httpFactory, settingsOverride ?? _settings, _cache, _authService);
    }

    private User SeedUserWithoutGitHub()
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = "testuser",
            DisplayName = "Test User",
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    // --- ListAsync ---

    [Fact]
    public async Task ListAsync_WithValidPat_ReturnsIssues()
    {
        // Raw JSON because the "+1" property name can't be expressed via anonymous objects
        var issuesJson = """
        [
            {
                "number": 1,
                "title": "Add dark mode",
                "body": "Please add dark mode support",
                "state": "open",
                "html_url": "https://github.com/test-owner/test-repo/issues/1",
                "user": { "login": "octocat" },
                "created_at": "2024-01-01T00:00:00Z",
                "labels": [{ "name": "enhancement" }],
                "reactions": { "+1": 5 }
            }
        ]
        """;

        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(issuesJson, Encoding.UTF8, "application/json")
        });

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Count.ShouldBe(1);
        result.Data[0].Title.ShouldBe("Add dark mode");
        result.Data[0].Number.ShouldBe(1);
        result.Data[0].Type.ShouldBe("feature");
        result.Data[0].AuthorLogin.ShouldBe("octocat");
        result.Data[0].VoteCount.ShouldBe(5);
    }

    [Fact]
    public async Task ListAsync_WithBugLabel_ReturnsBugType()
    {
        var issuesJson = """
        [
            {
                "number": 2,
                "title": "Crash on startup",
                "body": "App crashes",
                "state": "open",
                "html_url": "https://github.com/test-owner/test-repo/issues/2",
                "user": { "login": "dev" },
                "created_at": "2024-02-01T00:00:00Z",
                "labels": [{ "name": "bug" }],
                "reactions": { "+1": 0 }
            }
        ]
        """;

        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(issuesJson, Encoding.UTF8, "application/json")
        });

        var service = CreateService(handler);
        var result = await service.ListAsync("bug", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data![0].Type.ShouldBe("bug");
    }

    [Fact]
    public async Task ListAsync_WithEmptyPat_ReturnsNotConfiguredError()
    {
        var settings = Options.Create(new GitHubSettings
        {
            PersonalAccessToken = "",
            Owner = "owner",
            Repository = "repo",
        });

        var service = CreateService(settingsOverride: settings);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.GitHubNotConfigured);
    }

    [Fact]
    public async Task ListAsync_GitHubReturns401_ReturnsAuthFailedError()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubAuthFailed);
    }

    [Fact]
    public async Task ListAsync_GitHubReturns403_ReturnsRateLimitedError()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubRateLimited);
    }

    [Fact]
    public async Task ListAsync_GitHubReturns404_ReturnsIssueNotFoundError()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubIssueNotFound);
    }

    [Fact]
    public async Task ListAsync_GitHubReturns500_ReturnsApiUnavailableError()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubApiUnavailable);
    }

    [Fact]
    public async Task ListAsync_CachesResult_SecondCallDoesNotHitGitHub()
    {
        var callCount = 0;
        var handler = new MockHttpHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);

        await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);
        await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        callCount.ShouldBe(1); // Second call should use cache
    }

    [Fact]
    public async Task ListAsync_DifferentParams_MakesSeparateCalls()
    {
        var callCount = 0;
        var handler = new MockHttpHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);

        await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);
        await service.ListAsync("bug", 1, 20, "open", null, CancellationToken.None);

        callCount.ShouldBe(2); // Different type = different cache key
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyList_WhenNoIssues()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });

        var service = CreateService(handler);
        var result = await service.ListAsync("feature", 1, 20, "open", null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Count.ShouldBe(0);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_WithNoGitHubLink_ReturnsAccountNotLinkedError()
    {
        var user = SeedUserWithoutGitHub();

        var service = CreateService();
        var request = new CreateFeedbackRequest { Title = "Test", Description = "Test desc", Type = "bug" };
        var result = await service.CreateAsync(request, user.Id, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.GitHubAccountNotLinked);
    }

    [Fact]
    public async Task CreateAsync_UserNotInDb_ReturnsAccountNotLinkedError()
    {
        var service = CreateService();
        var request = new CreateFeedbackRequest { Title = "Test", Description = "desc", Type = "bug" };
        var result = await service.CreateAsync(request, Guid.NewGuid(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubAccountNotLinked);
    }

    // --- VoteAsync ---

    [Fact]
    public async Task VoteAsync_WithNoGitHubLink_ReturnsAccountNotLinkedError()
    {
        var user = SeedUserWithoutGitHub();

        var service = CreateService();
        var result = await service.VoteAsync(1, user.Id, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(ErrorCodes.GitHubAccountNotLinked);
    }

    [Fact]
    public async Task VoteAsync_UserNotInDb_ReturnsAccountNotLinkedError()
    {
        var service = CreateService();
        var result = await service.VoteAsync(1, Guid.NewGuid(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubAccountNotLinked);
    }

    // --- UnvoteAsync ---

    [Fact]
    public async Task UnvoteAsync_WithNoGitHubLink_ReturnsAccountNotLinkedError()
    {
        var user = SeedUserWithoutGitHub();

        var service = CreateService();
        var result = await service.UnvoteAsync(1, user.Id, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubAccountNotLinked);
    }

    // --- GetByNumberAsync ---

    [Fact]
    public async Task GetByNumberAsync_WithEmptyPat_ReturnsNotConfiguredError()
    {
        var settings = Options.Create(new GitHubSettings
        {
            PersonalAccessToken = "",
            Owner = "owner",
            Repository = "repo",
        });

        var service = CreateService(settingsOverride: settings);
        var result = await service.GetByNumberAsync(1, null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubNotConfigured);
    }

    [Fact]
    public async Task GetByNumberAsync_IssueNotFound_ReturnsError()
    {
        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = CreateService(handler);
        var result = await service.GetByNumberAsync(999, null, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.GitHubIssueNotFound);
    }

    [Fact]
    public async Task GetByNumberAsync_WithValidIssue_ReturnsDto()
    {
        var issueJson = """
        {
            "number": 42,
            "title": "Some issue",
            "body": "Issue body",
            "state": "open",
            "html_url": "https://github.com/test-owner/test-repo/issues/42",
            "user": { "login": "author" },
            "created_at": "2024-06-15T12:00:00Z",
            "labels": [{ "name": "bug" }],
            "reactions": { "+1": 3 }
        }
        """;

        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(issueJson, Encoding.UTF8, "application/json")
        });

        var service = CreateService(handler);
        var result = await service.GetByNumberAsync(42, null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Number.ShouldBe(42);
        result.Data.Title.ShouldBe("Some issue");
        result.Data.Type.ShouldBe("bug");
        result.Data.VoteCount.ShouldBe(3);
        result.Data.AuthorLogin.ShouldBe("author");
    }

    [Fact]
    public async Task GetByNumberAsync_TruncatesLongBody()
    {
        var longBody = new string('x', 1000);
        var issueJson = $$"""
        {
            "number": 1,
            "title": "Long body",
            "body": "{{longBody}}",
            "state": "open",
            "html_url": "https://github.com/test-owner/test-repo/issues/1",
            "user": { "login": "author" },
            "created_at": "2024-01-01T00:00:00Z",
            "labels": [{ "name": "enhancement" }],
            "reactions": { "+1": 0 }
        }
        """;

        var handler = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(issueJson, Encoding.UTF8, "application/json")
        });

        var service = CreateService(handler);
        var result = await service.GetByNumberAsync(1, null, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data!.Body.Length.ShouldBe(500);
    }

    // --- Helpers ---

    internal class MockHttpHandler : DelegatingHandler
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
