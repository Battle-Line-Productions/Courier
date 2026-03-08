using Courier.Features.Chains;
using Courier.Features.Engine;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Courier.Worker.Services;

public class JobQueueProcessor : BackgroundService
{
    private const int DefaultConcurrencyLimit = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobQueueProcessor> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public JobQueueProcessor(IServiceScopeFactory scopeFactory, ILogger<JobQueueProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobQueueProcessor started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processor loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("JobQueueProcessor stopping.");
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Read concurrency limit from system_settings
            var concurrencyLimit = DefaultConcurrencyLimit;
            await using (var limitCmd = new NpgsqlCommand(
                "SELECT value FROM system_settings WHERE key = 'job.concurrency_limit'",
                connection, transaction))
            {
                var limitValue = await limitCmd.ExecuteScalarAsync(ct);
                if (limitValue is string limitStr && int.TryParse(limitStr, out var parsed))
                {
                    concurrencyLimit = parsed;
                }
            }

            // Count currently running executions
            int runningCount;
            await using (var countCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM job_executions WHERE state = 'running'",
                connection, transaction))
            {
                runningCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
            }

            if (runningCount >= concurrencyLimit)
            {
                _logger.LogDebug(
                    "Concurrency limit reached ({Running}/{Limit}). Skipping dequeue.",
                    runningCount, concurrencyLimit);
                await transaction.CommitAsync(ct);
                return;
            }

            // Atomically claim the next queued execution using FOR UPDATE SKIP LOCKED
            Guid? executionId = null;
            Guid? jobId = null;
            await using (var dequeueCmd = new NpgsqlCommand(
                """
                SELECT id, job_id FROM job_executions
                WHERE state = 'queued'
                ORDER BY queued_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """,
                connection, transaction))
            {
                await using var reader = await dequeueCmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    executionId = reader.GetGuid(0);
                    jobId = reader.GetGuid(1);
                }
            }

            if (executionId is null)
            {
                await transaction.CommitAsync(ct);
                return;
            }

            _logger.LogInformation("Dequeued execution {ExecutionId} for job {JobId}", executionId, jobId);

            // Update state to running and set started_at
            await using (var updateCmd = new NpgsqlCommand(
                "UPDATE job_executions SET state = 'running', started_at = @startedAt WHERE id = @id",
                connection, transaction))
            {
                updateCmd.Parameters.AddWithValue("id", executionId.Value);
                updateCmd.Parameters.AddWithValue("startedAt", DateTime.UtcNow);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);

            // Execute the job outside the transaction
            var engine = scope.ServiceProvider.GetRequiredService<JobEngine>();
            await engine.ExecuteAsync(executionId.Value, ct);

            // After job completes, evaluate chain progress if this execution is part of a chain
            var orchestrator = scope.ServiceProvider.GetRequiredService<ChainOrchestrator>();
            await orchestrator.EvaluateChainProgressAsync(executionId.Value, ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
