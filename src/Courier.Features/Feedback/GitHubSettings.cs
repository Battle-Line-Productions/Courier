namespace Courier.Features.Feedback;

public class GitHubSettings
{
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string OAuthClientId { get; set; } = string.Empty;
    public string OAuthClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string Owner { get; set; } = "Battle-Line-Productions";
    public string Repository { get; set; } = "CourierMFT";
    public int CacheMinutes { get; set; } = 5;
}
