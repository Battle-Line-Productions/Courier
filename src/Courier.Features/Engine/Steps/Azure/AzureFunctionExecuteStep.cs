using System.Text.Json;
using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.AzureFunctions;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Azure;

public class AzureFunctionExecuteStep : IJobStep
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly AzureFunctionClient _functionClient;
    private readonly AppInsightsQueryService _appInsights;
    private readonly ILogger<AzureFunctionExecuteStep> _logger;

    public string TypeKey => "azure_function.execute";

    public AzureFunctionExecuteStep(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        AzureFunctionClient functionClient,
        AppInsightsQueryService appInsights,
        ILogger<AzureFunctionExecuteStep> logger)
    {
        _db = db;
        _encryptor = encryptor;
        _functionClient = functionClient;
        _appInsights = appInsights;
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

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (connection is null)
            return StepResult.Fail($"Connection '{connectionId}' not found.");

        if (connection.Protocol != "azure_function")
            return StepResult.Fail($"Connection '{connection.Name}' uses protocol '{connection.Protocol}', expected 'azure_function'.");

        // 2. Decrypt credentials
        if (connection.PasswordEncrypted is null)
            return StepResult.Fail("Connection is missing the master key (stored as password).");

        var masterKey = _encryptor.Decrypt(connection.PasswordEncrypted);

        if (connection.ClientSecretEncrypted is null)
            return StepResult.Fail("Connection is missing the client secret.");

        var clientSecret = _encryptor.Decrypt(connection.ClientSecretEncrypted);

        // 3. Parse properties
        if (string.IsNullOrEmpty(connection.Properties))
            return StepResult.Fail("Connection is missing properties (workspace_id, tenant_id, client_id).");

        JsonElement props;
        try
        {
            props = JsonDocument.Parse(connection.Properties).RootElement;
        }
        catch (JsonException ex)
        {
            return StepResult.Fail($"Failed to parse connection properties: {ex.Message}");
        }

        var workspaceId = props.TryGetProperty("workspace_id", out var wsId) ? wsId.GetString() : null;
        var tenantId = props.TryGetProperty("tenant_id", out var tid) ? tid.GetString() : null;
        var clientId = props.TryGetProperty("client_id", out var cid) ? cid.GetString() : null;

        if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
            return StepResult.Fail("Connection properties must include workspace_id, tenant_id, and client_id.");

        // 4. Read step config
        var functionName = config.GetString("function_name");
        var inputPayload = config.GetStringOrDefault("input_payload");
        var pollIntervalSec = config.GetIntOrDefault("poll_interval_sec", 15);
        var maxWaitSec = config.GetIntOrDefault("max_wait_sec", 3600);
        var initialDelaySec = config.GetIntOrDefault("initial_delay_sec", 30);

        // 5. Trigger function
        _logger.LogInformation("Triggering Azure Function '{FunctionName}' on '{Host}'", functionName, connection.Host);

        var triggerResult = await _functionClient.TriggerAsync(
            connection.Host, functionName, masterKey, inputPayload, cancellationToken);

        if (!triggerResult.Success)
            return StepResult.Fail($"Azure Function trigger failed: {triggerResult.ErrorMessage}");

        // 6. Acquire Entra token
        string token;
        try
        {
            token = await AppInsightsQueryService.AcquireTokenAsync(tenantId, clientId, clientSecret, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StepResult.Fail($"Failed to acquire Entra token: {ex.Message}");
        }

        // 7. Poll for completion
        var executionResult = await _appInsights.PollForCompletionAsync(
            workspaceId, token, functionName,
            triggerResult.TriggerTimeUtc!.Value,
            pollIntervalSec, maxWaitSec, initialDelaySec,
            cancellationToken);

        if (executionResult is null)
            return StepResult.Fail($"Azure Function '{functionName}' did not complete within {maxWaitSec}s.");

        // 8. Return result with outputs for downstream steps
        var outputs = new Dictionary<string, object>
        {
            ["invocation_id"] = executionResult.InvocationId ?? "",
            ["operation_id"] = executionResult.OperationId ?? "",
            ["function_success"] = executionResult.Success,
            ["function_duration_ms"] = executionResult.DurationMs ?? 0
        };

        if (!executionResult.Success)
        {
            return StepResult.Fail(
                $"Azure Function '{functionName}' completed but reported failure (invocationId={executionResult.InvocationId}).");
        }

        _logger.LogInformation(
            "Azure Function '{FunctionName}' completed successfully in {Duration}ms",
            functionName, executionResult.DurationMs);

        return StepResult.Ok(outputs: outputs);
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
