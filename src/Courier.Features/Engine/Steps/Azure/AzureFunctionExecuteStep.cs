using System.Net.Http.Json;
using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Domain.Encryption;
using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Azure;

public class AzureFunctionExecuteStep : IJobStep
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly StepCallbackService _callbackService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureFunctionExecuteStep> _logger;

    public string TypeKey => "azure_function.execute";

    public AzureFunctionExecuteStep(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        StepCallbackService callbackService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AzureFunctionExecuteStep> logger)
    {
        _db = db;
        _encryptor = encryptor;
        _callbackService = callbackService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // 1. Resolve connection
        var connectionIdStr = config.GetString("connection_id");
        if (!Guid.TryParse(connectionIdStr, out var connectionId))
            return StepResult.Fail($"Invalid connection_id: {connectionIdStr}");

        var connection = await _db.Connections.FirstOrDefaultAsync(
            c => c.Id == connectionId, cancellationToken);
        if (connection is null)
            return StepResult.Fail($"Connection '{connectionId}' not found.");
        if (connection.Protocol != "azure_function")
            return StepResult.Fail($"Connection '{connection.Name}' uses protocol '{connection.Protocol}', expected 'azure_function'.");

        // 2. Decrypt function key
        if (connection.PasswordEncrypted is null)
            return StepResult.Fail("Connection is missing the function key (stored as password).");
        var functionKey = _encryptor.Decrypt(connection.PasswordEncrypted);

        // 3. Read step config
        var functionName = config.GetString("function_name");
        var inputPayload = config.GetStringOrDefault("input_payload");
        var waitForCallback = config.GetBoolOrDefault("wait_for_callback", true);
        var maxWaitSec = config.GetIntOrDefault("max_wait_sec", 3600);
        var pollIntervalSec = config.GetIntOrDefault("poll_interval_sec", 5);

        // 4. Build request
        var url = $"https://{connection.Host}/api/{functionName}?code={functionKey}";
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        if (!waitForCallback)
            return await ExecuteFireAndForgetAsync(client, url, inputPayload, functionName, cancellationToken);

        return await ExecuteWithCallbackAsync(
            client, url, inputPayload, functionName,
            maxWaitSec, pollIntervalSec, cancellationToken);
    }

    private async Task<StepResult> ExecuteFireAndForgetAsync(
        HttpClient client, string url, string? inputPayload,
        string functionName, CancellationToken ct)
    {
        _logger.LogInformation("Triggering Azure Function '{FunctionName}' (fire-and-forget)", functionName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = inputPayload is not null
                ? new StringContent(inputPayload, System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(new { });

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return StepResult.Fail($"Azure Function returned HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Azure Function '{FunctionName}' triggered successfully", functionName);
            return StepResult.Ok(outputs: new Dictionary<string, object>
            {
                ["function_success"] = true,
                ["http_status"] = (int)response.StatusCode,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StepResult.Fail($"Failed to trigger Azure Function: {ex.Message}");
        }
    }

    private async Task<StepResult> ExecuteWithCallbackAsync(
        HttpClient client, string url, string? inputPayload,
        string functionName, int maxWaitSec, int pollIntervalSec,
        CancellationToken ct)
    {
        var baseUrl = _configuration["Courier:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
            return StepResult.Fail("Courier:BaseUrl configuration is required for Azure Function callback mode.");

        // Create callback record
        var (callbackId, callbackKey) = await _callbackService.CreateAsync(maxWaitSec, ct);

        _logger.LogInformation(
            "Triggering Azure Function '{FunctionName}' with callback {CallbackId}",
            functionName, callbackId);

        // Build request body with callback info
        var callbackUrl = $"{baseUrl.TrimEnd('/')}/api/v1/callbacks/{callbackId}";
        object body;
        if (inputPayload is not null)
        {
            try
            {
                var payloadElement = JsonDocument.Parse(inputPayload).RootElement;
                body = new { payload = payloadElement, callback = new { url = callbackUrl, key = callbackKey } };
            }
            catch (JsonException)
            {
                body = new { payload = inputPayload, callback = new { url = callbackUrl, key = callbackKey } };
            }
        }
        else
        {
            body = new { callback = new { url = callbackUrl, key = callbackKey } };
        }

        // Trigger function
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = JsonContent.Create(body);

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                await _callbackService.DeleteAsync(callbackId, ct);
                return StepResult.Fail($"Azure Function returned HTTP {(int)response.StatusCode}: {responseBody}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _callbackService.DeleteAsync(callbackId, ct);
            return StepResult.Fail($"Failed to trigger Azure Function: {ex.Message}");
        }

        // Poll for callback completion
        _logger.LogInformation("Waiting for callback from '{FunctionName}' (max {MaxWait}s)", functionName, maxWaitSec);

        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSec);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var callback = await _callbackService.GetByIdAsync(callbackId, ct);

            if (callback is null)
                return StepResult.Fail("Callback record was unexpectedly deleted.");

            if (callback.Status == "completed")
            {
                _logger.LogInformation("Azure Function '{FunctionName}' completed successfully via callback", functionName);

                var outputs = new Dictionary<string, object>
                {
                    ["function_success"] = true,
                };

                if (callback.ResultPayload is not null)
                {
                    try
                    {
                        var resultElement = JsonDocument.Parse(callback.ResultPayload).RootElement;
                        outputs["callback_result"] = resultElement;
                    }
                    catch (JsonException)
                    {
                        outputs["callback_result"] = callback.ResultPayload;
                    }
                }

                return StepResult.Ok(outputs: outputs);
            }

            if (callback.Status == "failed")
            {
                return StepResult.Fail(
                    $"Azure Function '{functionName}' reported failure: {callback.ErrorMessage ?? "no error message"}");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSec), ct);
        }

        // Timeout
        await _callbackService.MarkExpiredAsync(callbackId, ct);
        return StepResult.Fail($"Azure Function '{functionName}' did not call back within {maxWaitSec}s.");
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("connection_id"))
            return Task.FromResult(StepResult.Fail("Missing required config: connection_id"));
        if (!config.Has("function_name"))
            return Task.FromResult(StepResult.Fail("Missing required config: function_name"));
        return Task.FromResult(StepResult.Ok());
    }
}
