using System.Text.Json.Serialization;

namespace Courier.Functions.Sdk;

internal class CourierCallbackPayload
{
    [JsonPropertyName("payload")]
    public System.Text.Json.JsonElement? Payload { get; set; }

    [JsonPropertyName("callback")]
    public CallbackInfo? Callback { get; set; }
}

internal class CallbackInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}
