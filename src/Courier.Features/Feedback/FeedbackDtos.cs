namespace Courier.Features.Feedback;

public record FeedbackItemDto
{
    public int Number { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string Type { get; init; }
    public required string State { get; init; }
    public int VoteCount { get; init; }
    public bool HasVoted { get; init; }
    public required string Url { get; init; }
    public required string AuthorLogin { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
}

public record CreateFeedbackRequest
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
}

public record FeedbackVoteResponse
{
    public int Number { get; init; }
    public bool Voted { get; init; }
    public int VoteCount { get; init; }
}

public record GitHubOAuthUrlResponse
{
    public required string Url { get; init; }
}

public record GitHubCallbackRequest
{
    public required string Code { get; init; }
}

public record GitHubLinkResponse
{
    public required string GitHubUsername { get; init; }
}
