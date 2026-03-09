using Courier.Domain.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Courier.Features.Feedback;

public class FeedbackService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly GitHubAuthService _authService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly ConcurrentBag<string> CachedKeys = [];

    public FeedbackService(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubSettings> settings,
        IMemoryCache cache,
        GitHubAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _cache = cache;
        _authService = authService;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string GetLabel(string type) => type == "bug" ? "bug" : "enhancement";

    private static string GetTypeFromLabels(JsonElement labelsElement)
    {
        foreach (var label in labelsElement.EnumerateArray())
        {
            var name = label.GetProperty("name").GetString() ?? string.Empty;

            if (name.Equals("bug", StringComparison.OrdinalIgnoreCase))
                return "bug";

            if (name.Equals("enhancement", StringComparison.OrdinalIgnoreCase))
                return "feature";
        }

        return "feature";
    }

    private static FeedbackItemDto MapIssue(JsonElement issue, long? currentUserGitHubId, JsonElement? reactionsDetail)
    {
        var body = issue.GetProperty("body").GetString() ?? string.Empty;

        if (body.Length > 500)
            body = body[..500];

        var labels = new List<string>();
        foreach (var label in issue.GetProperty("labels").EnumerateArray())
        {
            labels.Add(label.GetProperty("name").GetString() ?? string.Empty);
        }

        var voteCount = 0;
        if (issue.TryGetProperty("reactions", out var reactions) &&
            reactions.TryGetProperty("+1", out var plusOne))
        {
            voteCount = plusOne.GetInt32();
        }

        var hasVoted = false;
        if (currentUserGitHubId.HasValue && reactionsDetail.HasValue)
        {
            foreach (var reaction in reactionsDetail.Value.EnumerateArray())
            {
                var content = reaction.GetProperty("content").GetString();
                var userId = reaction.GetProperty("user").GetProperty("id").GetInt64();

                if (content == "+1" && userId == currentUserGitHubId.Value)
                {
                    hasVoted = true;
                    break;
                }
            }
        }

        return new FeedbackItemDto
        {
            Number = issue.GetProperty("number").GetInt32(),
            Title = issue.GetProperty("title").GetString() ?? string.Empty,
            Body = body,
            Type = GetTypeFromLabels(issue.GetProperty("labels")),
            State = issue.GetProperty("state").GetString() ?? "open",
            VoteCount = voteCount,
            HasVoted = hasVoted,
            Url = issue.GetProperty("html_url").GetString() ?? string.Empty,
            AuthorLogin = issue.GetProperty("user").GetProperty("login").GetString() ?? string.Empty,
            CreatedAt = issue.GetProperty("created_at").GetDateTime(),
            Labels = labels
        };
    }

    private static ApiResponse<T> MapGitHubError<T>(HttpStatusCode status)
    {
        var (code, message) = status switch
        {
            HttpStatusCode.Unauthorized => (ErrorCodes.GitHubAuthFailed, "GitHub authentication failed."),
            HttpStatusCode.Forbidden => (ErrorCodes.GitHubRateLimited, "GitHub API rate limit exceeded."),
            HttpStatusCode.NotFound => (ErrorCodes.GitHubIssueNotFound, "GitHub issue not found."),
            _ => (ErrorCodes.GitHubApiUnavailable, $"GitHub API returned status {(int)status}.")
        };

        return new ApiResponse<T>
        {
            Error = ErrorMessages.Create(code, message)
        };
    }

    private void InvalidateCache()
    {
        var keys = CachedKeys.ToArray();

        foreach (var key in keys)
        {
            _cache.Remove(key);
        }

        // Clear the bag — concurrent replace with a new drained state
        while (CachedKeys.TryTake(out _)) { }
    }

    private void CacheSet<T>(string key, T value)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.CacheMinutes)
        };

        _cache.Set(key, value, options);
        CachedKeys.Add(key);
    }

    public async Task<ApiResponse<List<FeedbackItemDto>>> ListAsync(
        string type, int page, int pageSize, string state, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_settings.PersonalAccessToken))
        {
            return new ApiResponse<List<FeedbackItemDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubNotConfigured, "GitHub integration is not configured.")
            };
        }

        var cacheKey = $"feedback:{type}:{page}:{pageSize}:{state}";

        if (_cache.TryGetValue<List<FeedbackItemDto>>(cacheKey, out var cached) && cached is not null)
        {
            return new ApiResponse<List<FeedbackItemDto>> { Data = cached };
        }

        var label = GetLabel(type);
        var client = CreateAuthenticatedClient(_settings.PersonalAccessToken);

        var url = $"repos/{_settings.Owner}/{_settings.Repository}/issues" +
                  $"?labels={label}&state={state}&page={page}&per_page={pageSize}&sort=created&direction=desc";

        var response = await client.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return MapGitHubError<List<FeedbackItemDto>>(response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var items = new List<FeedbackItemDto>();

        foreach (var issue in doc.RootElement.EnumerateArray())
        {
            items.Add(MapIssue(issue, null, null));
        }

        CacheSet(cacheKey, items);

        return new ApiResponse<List<FeedbackItemDto>> { Data = items };
    }

    public async Task<ApiResponse<FeedbackItemDto>> GetByNumberAsync(int number, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_settings.PersonalAccessToken))
        {
            return new ApiResponse<FeedbackItemDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubNotConfigured, "GitHub integration is not configured.")
            };
        }

        var cacheKey = $"feedback:item:{number}";

        if (_cache.TryGetValue<FeedbackItemDto>(cacheKey, out var cached) && cached is not null)
        {
            return new ApiResponse<FeedbackItemDto> { Data = cached };
        }

        var client = CreateAuthenticatedClient(_settings.PersonalAccessToken);

        var url = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}";
        var response = await client.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return MapGitHubError<FeedbackItemDto>(response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var issueDoc = JsonDocument.Parse(json);

        // Check reactions for HasVoted detection
        JsonElement? reactionsDetail = null;
        long? currentUserGitHubId = null;

        if (userId.HasValue)
        {
            var userToken = await _authService.GetDecryptedTokenAsync(userId.Value, ct);

            if (userToken is not null)
            {
                var userClient = CreateAuthenticatedClient(userToken);
                var reactionsUrl = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}/reactions";
                var reactionsResponse = await userClient.GetAsync(reactionsUrl, ct);

                if (reactionsResponse.IsSuccessStatusCode)
                {
                    var reactionsJson = await reactionsResponse.Content.ReadAsStringAsync(ct);
                    var reactionsDoc = JsonDocument.Parse(reactionsJson);
                    reactionsDetail = reactionsDoc.RootElement.Clone();

                    // Get user's GitHub ID from their profile
                    var profileResponse = await userClient.GetAsync("user", ct);

                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var profileJson = await profileResponse.Content.ReadAsStringAsync(ct);
                        using var profileDoc = JsonDocument.Parse(profileJson);
                        currentUserGitHubId = profileDoc.RootElement.GetProperty("id").GetInt64();
                    }
                }
            }
        }

        var item = MapIssue(issueDoc.RootElement, currentUserGitHubId, reactionsDetail);

        CacheSet(cacheKey, item);

        return new ApiResponse<FeedbackItemDto> { Data = item };
    }

    public async Task<ApiResponse<FeedbackItemDto>> CreateAsync(
        CreateFeedbackRequest request, Guid userId, CancellationToken ct)
    {
        var token = await _authService.GetDecryptedTokenAsync(userId, ct);

        if (token is null)
        {
            return new ApiResponse<FeedbackItemDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubAccountNotLinked, "GitHub account is not linked. Please link your GitHub account first.")
            };
        }

        var client = CreateAuthenticatedClient(token);

        var payload = new
        {
            title = request.Title,
            body = request.Description,
            labels = new[] { GetLabel(request.Type) }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var url = $"repos/{_settings.Owner}/{_settings.Repository}/issues";
        var response = await client.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
            return MapGitHubError<FeedbackItemDto>(response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        InvalidateCache();

        var item = MapIssue(doc.RootElement, null, null);

        return new ApiResponse<FeedbackItemDto> { Data = item };
    }

    public async Task<ApiResponse<FeedbackVoteResponse>> VoteAsync(int number, Guid userId, CancellationToken ct)
    {
        var token = await _authService.GetDecryptedTokenAsync(userId, ct);

        if (token is null)
        {
            return new ApiResponse<FeedbackVoteResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubAccountNotLinked, "GitHub account is not linked. Please link your GitHub account first.")
            };
        }

        var client = CreateAuthenticatedClient(token);

        var payload = new { content = "+1" };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var url = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}/reactions";
        var response = await client.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
            return MapGitHubError<FeedbackVoteResponse>(response.StatusCode);

        InvalidateCache();

        // Fetch updated issue to get current vote count
        var patClient = CreateAuthenticatedClient(_settings.PersonalAccessToken);
        var issueUrl = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}";
        var issueResponse = await patClient.GetAsync(issueUrl, ct);

        var voteCount = 0;

        if (issueResponse.IsSuccessStatusCode)
        {
            var issueJson = await issueResponse.Content.ReadAsStringAsync(ct);
            using var issueDoc = JsonDocument.Parse(issueJson);

            if (issueDoc.RootElement.TryGetProperty("reactions", out var reactions) &&
                reactions.TryGetProperty("+1", out var plusOne))
            {
                voteCount = plusOne.GetInt32();
            }
        }

        return new ApiResponse<FeedbackVoteResponse>
        {
            Data = new FeedbackVoteResponse
            {
                Number = number,
                Voted = true,
                VoteCount = voteCount
            }
        };
    }

    public async Task<ApiResponse<FeedbackVoteResponse>> UnvoteAsync(int number, Guid userId, CancellationToken ct)
    {
        var token = await _authService.GetDecryptedTokenAsync(userId, ct);

        if (token is null)
        {
            return new ApiResponse<FeedbackVoteResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubAccountNotLinked, "GitHub account is not linked. Please link your GitHub account first.")
            };
        }

        var client = CreateAuthenticatedClient(token);

        // Find the user's +1 reaction
        var reactionsUrl = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}/reactions";
        var reactionsResponse = await client.GetAsync(reactionsUrl, ct);

        if (!reactionsResponse.IsSuccessStatusCode)
            return MapGitHubError<FeedbackVoteResponse>(reactionsResponse.StatusCode);

        var reactionsJson = await reactionsResponse.Content.ReadAsStringAsync(ct);
        using var reactionsDoc = JsonDocument.Parse(reactionsJson);

        // Get the user's GitHub ID
        var profileResponse = await client.GetAsync("user", ct);

        if (!profileResponse.IsSuccessStatusCode)
            return MapGitHubError<FeedbackVoteResponse>(profileResponse.StatusCode);

        var profileJson = await profileResponse.Content.ReadAsStringAsync(ct);
        using var profileDoc = JsonDocument.Parse(profileJson);
        var gitHubUserId = profileDoc.RootElement.GetProperty("id").GetInt64();

        // Find and delete the +1 reaction
        long? reactionId = null;

        foreach (var reaction in reactionsDoc.RootElement.EnumerateArray())
        {
            var reactionContent = reaction.GetProperty("content").GetString();
            var reactionUserId = reaction.GetProperty("user").GetProperty("id").GetInt64();

            if (reactionContent == "+1" && reactionUserId == gitHubUserId)
            {
                reactionId = reaction.GetProperty("id").GetInt64();
                break;
            }
        }

        if (reactionId is null)
        {
            return new ApiResponse<FeedbackVoteResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubIssueNotFound, "No vote found to remove.")
            };
        }

        var deleteUrl = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}/reactions/{reactionId}";
        var deleteResponse = await client.DeleteAsync(deleteUrl, ct);

        if (!deleteResponse.IsSuccessStatusCode)
            return MapGitHubError<FeedbackVoteResponse>(deleteResponse.StatusCode);

        InvalidateCache();

        // Fetch updated issue to get current vote count
        var patClient = CreateAuthenticatedClient(_settings.PersonalAccessToken);
        var issueUrl = $"repos/{_settings.Owner}/{_settings.Repository}/issues/{number}";
        var issueResponse = await patClient.GetAsync(issueUrl, ct);

        var voteCount = 0;

        if (issueResponse.IsSuccessStatusCode)
        {
            var issueJson = await issueResponse.Content.ReadAsStringAsync(ct);
            using var issueDoc = JsonDocument.Parse(issueJson);

            if (issueDoc.RootElement.TryGetProperty("reactions", out var reactions) &&
                reactions.TryGetProperty("+1", out var plusOne))
            {
                voteCount = plusOne.GetInt32();
            }
        }

        return new ApiResponse<FeedbackVoteResponse>
        {
            Data = new FeedbackVoteResponse
            {
                Number = number,
                Voted = false,
                VoteCount = voteCount
            }
        };
    }
}
