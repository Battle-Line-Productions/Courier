using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Courier.Features.Feedback;

public class GitHubAuthService
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubSettings _settings;

    public GitHubAuthService(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubSettings> settings)
    {
        _db = db;
        _encryptor = encryptor;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public ApiResponse<GitHubOAuthUrlResponse> GetOAuthUrl(string state)
    {
        if (string.IsNullOrEmpty(_settings.OAuthClientId))
        {
            return new ApiResponse<GitHubOAuthUrlResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubNotConfigured, "GitHub OAuth is not configured.")
            };
        }

        var url = $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(_settings.OAuthClientId)}&scope=repo&state={Uri.EscapeDataString(state)}";

        if (!string.IsNullOrEmpty(_settings.CallbackUrl))
            url += $"&redirect_uri={Uri.EscapeDataString(_settings.CallbackUrl)}";

        return new ApiResponse<GitHubOAuthUrlResponse>
        {
            Data = new GitHubOAuthUrlResponse { Url = url }
        };
    }

    private async Task<string?> ExchangeCodeForTokenAsync(string code, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Courier-Feedback");

            var payload = new
            {
                client_id = _settings.OAuthClientId,
                client_secret = _settings.OAuthClientSecret,
                code
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://github.com/login/oauth/access_token", content, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("access_token", out var tokenElement)
                ? tokenElement.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(long id, string login)?> GetGitHubUserAsync(string token, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("user", ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var id = doc.RootElement.GetProperty("id").GetInt64();
            var login = doc.RootElement.GetProperty("login").GetString()!;

            return (id, login);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<GitHubLinkResponse>> LinkAccountAsync(Guid userId, string code, CancellationToken ct)
    {
        var token = await ExchangeCodeForTokenAsync(code, ct);

        if (token is null)
        {
            return new ApiResponse<GitHubLinkResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubOAuthFailed, "Failed to exchange authorization code for token.")
            };
        }

        var ghUser = await GetGitHubUserAsync(token, ct);

        if (ghUser is null)
        {
            return new ApiResponse<GitHubLinkResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.GitHubOAuthFailed, "Failed to retrieve GitHub user information.")
            };
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return new ApiResponse<GitHubLinkResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
            };
        }

        user.GitHubId = ghUser.Value.id;
        user.GitHubUsername = ghUser.Value.login;
        user.GitHubToken = _encryptor.Encrypt(token);
        user.GitHubLinkedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<GitHubLinkResponse>
        {
            Data = new GitHubLinkResponse { GitHubUsername = ghUser.Value.login }
        };
    }

    public async Task<ApiResponse> UnlinkAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
            };
        }

        user.GitHubId = null;
        user.GitHubUsername = null;
        user.GitHubToken = null;
        user.GitHubLinkedAt = null;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse();
    }

    public async Task<string?> GetDecryptedTokenAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user?.GitHubToken is null)
            return null;

        return _encryptor.Decrypt(user.GitHubToken);
    }
}
