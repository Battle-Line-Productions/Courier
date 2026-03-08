namespace Courier.Features.Keys;

public record CreateShareLinkRequest(int? ExpiryDays);

public record ShareLinkResponse(Guid LinkId, string Token, DateTime ExpiresAt);

public record SharedKeyResponse(string PublicKey, string KeyType, string? Name);
