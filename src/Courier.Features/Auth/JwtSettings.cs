namespace Courier.Features.Auth;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "courier";
    public string Audience { get; set; } = "courier-api";
}
