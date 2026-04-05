using System.Text.Json;

namespace Courier.Features.Callbacks;

public record CallbackRequest
{
    public bool Success { get; init; } = true;
    public JsonElement? Output { get; init; }
    public string? ErrorMessage { get; init; }
}
