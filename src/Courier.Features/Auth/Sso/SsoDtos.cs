namespace Courier.Features.Auth.Sso;

public record SsoExchangeRequest(string Code);

public record SsoExchangeCodeData(Guid UserId, Guid ProviderId, DateTime CreatedAt);

public record SsoInitiateResult(string RedirectUrl);
