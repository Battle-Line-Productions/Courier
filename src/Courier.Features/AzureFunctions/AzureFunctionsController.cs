using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Features.Connections;
using Courier.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Courier.Features.AzureFunctions;

[ApiController]
[Route("api/v1/azure-functions")]
public class AzureFunctionsController : ControllerBase
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly AppInsightsQueryService _appInsights;

    public AzureFunctionsController(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        AppInsightsQueryService appInsights)
    {
        _db = db;
        _encryptor = encryptor;
        _appInsights = appInsights;
    }

    [HttpGet("{connectionId:guid}/traces/{invocationId}")]
    public async Task<ActionResult<ApiResponse<List<AzureFunctionTraceDto>>>> GetTraces(
        Guid connectionId,
        string invocationId,
        CancellationToken ct)
    {
        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);

        if (connection is null)
        {
            return NotFound(new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{connectionId}' not found.")
            });
        }

        if (connection.Protocol != "azure_function")
        {
            return BadRequest(new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidAzureFunctionConfig,
                    $"Connection '{connection.Name}' uses protocol '{connection.Protocol}', expected 'azure_function'.")
            });
        }

        if (connection.ClientSecretEncrypted is null || string.IsNullOrEmpty(connection.Properties))
        {
            return BadRequest(new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidAzureFunctionConfig,
                    "Connection is missing required credentials or properties.")
            });
        }

        JsonElement props;
        try
        {
            props = JsonDocument.Parse(connection.Properties).RootElement;
        }
        catch (JsonException)
        {
            return BadRequest(new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidAzureFunctionConfig, "Failed to parse connection properties.")
            });
        }

        var workspaceId = props.TryGetProperty("workspace_id", out var wsId) ? wsId.GetString() : null;
        var tenantId = props.TryGetProperty("tenant_id", out var tid) ? tid.GetString() : null;
        var clientId = props.TryGetProperty("client_id", out var cid) ? cid.GetString() : null;

        if (string.IsNullOrEmpty(workspaceId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
        {
            return BadRequest(new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.InvalidAzureFunctionConfig,
                    "Connection properties must include workspace_id, tenant_id, and client_id.")
            });
        }

        var clientSecret = _encryptor.Decrypt(connection.ClientSecretEncrypted);

        string token;
        try
        {
            token = await _appInsights.AcquireTokenAsync(tenantId, clientId, clientSecret, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(502, new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.EntraTokenAcquisitionFailed, ex.Message)
            });
        }

        try
        {
            var traces = await _appInsights.GetTracesAsync(workspaceId, token, invocationId, ct);
            return Ok(new ApiResponse<List<AzureFunctionTraceDto>> { Data = traces });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(502, new ApiResponse<List<AzureFunctionTraceDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.AppInsightsQueryFailed, ex.Message)
            });
        }
    }
}
