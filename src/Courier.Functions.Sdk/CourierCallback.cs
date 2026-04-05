using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Courier.Functions.Sdk;

/// <summary>
/// Extracts callback information from a Courier-triggered HTTP request
/// and provides methods to report function completion.
/// </summary>
public class CourierCallback
{
    private readonly string? _callbackUrl;
    private readonly string? _callbackKey;

    private CourierCallback(string? callbackUrl, string? callbackKey, JsonElement? payload)
    {
        _callbackUrl = callbackUrl;
        _callbackKey = callbackKey;
        Payload = payload;
    }

    /// <summary>
    /// The user's payload from the Courier job step configuration.
    /// Null if no payload was provided.
    /// </summary>
    public JsonElement? Payload { get; }

    /// <summary>
    /// Whether this instance has callback info (true) or is a no-op (false).
    /// When false, SuccessAsync/FailAsync are silent no-ops.
    /// </summary>
    public bool HasCallback => _callbackUrl != null;

    /// <summary>
    /// Extracts callback info from the HTTP request body.
    /// Returns a no-op instance if no callback info is present (fire-and-forget mode).
    /// </summary>
    public static CourierCallback FromBody(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new CourierCallback(null, null, null);

        try
        {
            var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            JsonElement? payload = null;
            if (root.TryGetProperty("payload", out var p))
                payload = p.Clone();

            if (root.TryGetProperty("callback", out var cb)
                && cb.TryGetProperty("url", out var urlProp)
                && cb.TryGetProperty("key", out var keyProp))
            {
                var url = urlProp.GetString();
                var key = keyProp.GetString();

                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(key))
                    return new CourierCallback(url, key, payload);
            }

            // No callback info — might be fire-and-forget or raw payload
            return new CourierCallback(null, null, root.Clone());
        }
        catch (JsonException)
        {
            return new CourierCallback(null, null, null);
        }
    }

    /// <summary>
    /// Reports successful completion to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public async Task SuccessAsync(object? output = null, CancellationToken ct = default)
    {
        if (!HasCallback) return;
        await SendAsync(true, output, null, ct);
    }

    /// <summary>
    /// Reports failure to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public async Task FailAsync(string errorMessage, CancellationToken ct = default)
    {
        if (!HasCallback) return;
        await SendAsync(false, null, errorMessage, ct);
    }

    private async Task SendAsync(bool success, object? output, string? errorMessage, CancellationToken ct)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _callbackUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _callbackKey);

        var body = new { success, output, errorMessage };
        var json = JsonSerializer.Serialize(body);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        await client.SendAsync(request, ct);
    }
}
