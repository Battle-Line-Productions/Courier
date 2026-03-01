using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Courier.Features.AzureFunctions;

public class AzureFunctionClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureFunctionClient> _logger;

    public AzureFunctionClient(IHttpClientFactory httpClientFactory, ILogger<AzureFunctionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public virtual async Task<AzureFunctionTriggerResult> TriggerAsync(
        string host,
        string functionName,
        string masterKey,
        string? inputPayload,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        var url = $"https://{host}/admin/functions/{functionName}";
        var triggerTime = DateTime.UtcNow;

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-functions-key", masterKey);

        var body = new { input = inputPayload ?? "" };
        request.Content = JsonContent.Create(body);

        try
        {
            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted ||
                response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Azure Function '{FunctionName}' triggered successfully at {TriggerTime}",
                    functionName, triggerTime);

                return new AzureFunctionTriggerResult(true, triggerTime);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Azure Function trigger failed with status {StatusCode}: {Response}",
                response.StatusCode, responseBody);

            return new AzureFunctionTriggerResult(
                false, null,
                $"Trigger returned HTTP {(int)response.StatusCode}: {responseBody}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to trigger Azure Function '{FunctionName}'", functionName);
            return new AzureFunctionTriggerResult(false, null, ex.Message);
        }
    }
}
