using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Courier.Features.AzureFunctions;

public class AppInsightsQueryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AppInsightsQueryService> _logger;

    private static readonly string[] LogAnalyticsScope = ["https://api.loganalytics.io/.default"];

    public AppInsightsQueryService(IHttpClientFactory httpClientFactory, ILogger<AppInsightsQueryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public static async Task<string> AcquireTokenAsync(string tenantId, string clientId, string clientSecret, CancellationToken ct)
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        var tokenResult = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(LogAnalyticsScope), ct);
        return tokenResult.Token;
    }

    public virtual async Task<FunctionExecutionResult?> PollForCompletionAsync(
        string workspaceId,
        string token,
        string functionName,
        DateTime triggerTimeUtc,
        int pollIntervalSec,
        int maxWaitSec,
        int initialDelaySec,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Waiting {InitialDelay}s before first poll for function '{FunctionName}'",
            initialDelaySec, functionName);

        await Task.Delay(TimeSpan.FromSeconds(initialDelaySec), ct);

        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSec);
        var triggerTimeStr = triggerTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var kql = $"""
            requests
            | where name == '{functionName}'
            | where timestamp > datetime({triggerTimeStr})
            | project timestamp, name, duration, success, customDimensions.InvocationId, operation_Id
            | order by timestamp desc
            | take 1
            """;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var result = await ExecuteQueryAsync(workspaceId, token, kql, ct);

            if (result is not null)
            {
                var rows = GetRows(result.Value);
                if (rows.Count > 0)
                {
                    var row = rows[0];
                    var success = row.Count > 3 && row[3].GetString() == "True";
                    var durationMs = row.Count > 2 ? row[2].GetDouble() : 0;
                    var invocationId = row.Count > 4 ? row[4].GetString() : null;
                    var operationId = row.Count > 5 ? row[5].GetString() : null;

                    _logger.LogInformation(
                        "Function '{FunctionName}' completed: success={Success}, duration={Duration}ms, invocationId={InvocationId}",
                        functionName, success, durationMs, invocationId);

                    return new FunctionExecutionResult(success, durationMs, invocationId, operationId);
                }
            }

            _logger.LogDebug("Function '{FunctionName}' not yet complete, polling again in {Interval}s", functionName, pollIntervalSec);
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSec), ct);
        }

        _logger.LogWarning("Polling timed out for function '{FunctionName}' after {MaxWait}s", functionName, maxWaitSec);
        return null;
    }

    public virtual async Task<List<AzureFunctionTraceDto>> GetTracesAsync(
        string workspaceId,
        string token,
        string invocationId,
        CancellationToken ct)
    {
        var kql = $"""
            traces
            | where customDimensions.InvocationId == '{invocationId}'
            | project timestamp, message, severityLevel
            | order by timestamp asc
            """;

        var result = await ExecuteQueryAsync(workspaceId, token, kql, ct);
        var traces = new List<AzureFunctionTraceDto>();

        if (result is null)
            return traces;

        var rows = GetRows(result.Value);
        foreach (var row in rows)
        {
            if (row.Count >= 3)
            {
                traces.Add(new AzureFunctionTraceDto
                {
                    Timestamp = row[0].GetDateTime(),
                    Message = row[1].GetString() ?? "",
                    SeverityLevel = row[2].GetInt32()
                });
            }
        }

        return traces;
    }

    private async Task<JsonElement?> ExecuteQueryAsync(
        string workspaceId,
        string token,
        string kql,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("LogAnalytics");
        var url = $"https://api.loganalytics.azure.com/v1/workspaces/{workspaceId}/query";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { query = kql });

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Log Analytics query failed with {StatusCode}: {Body}",
                response.StatusCode, body);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(json).RootElement;
    }

    private static List<List<JsonElement>> GetRows(JsonElement result)
    {
        var rows = new List<List<JsonElement>>();

        if (result.TryGetProperty("tables", out var tables))
        {
            foreach (var table in tables.EnumerateArray())
            {
                if (table.TryGetProperty("rows", out var tableRows))
                {
                    foreach (var row in tableRows.EnumerateArray())
                    {
                        rows.Add(row.EnumerateArray().ToList());
                    }
                }
            }
        }

        return rows;
    }
}
