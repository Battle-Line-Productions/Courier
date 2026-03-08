using Npgsql;

namespace Courier.Worker.Services;

public class PartitionMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PartitionMaintenanceService> _logger;
    private readonly IConfiguration _configuration;

    public PartitionMaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<PartitionMaintenanceService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PartitionMaintenanceService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMaintenanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in partition maintenance");
            }

            var delay = GetDelayUntilNextRun(DateTime.UtcNow);
            _logger.LogInformation("Next partition maintenance run in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("PartitionMaintenanceService stopping");
    }

    private async Task RunMaintenanceAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running partition maintenance...");

        var connectionString = _configuration.GetConnectionString("CourierDb");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var now = DateTime.UtcNow;

        // Create audit log partitions for current month + next 2 months
        for (var i = 0; i < 3; i++)
        {
            var targetDate = now.AddMonths(i);
            await using var cmd = new NpgsqlCommand("SELECT create_monthly_partitions($1::date)", connection);
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateOnly.FromDateTime(targetDate).ToDateTime(TimeOnly.MinValue) });
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogDebug("Ensured audit partition exists for {Month}", targetDate.ToString("yyyy-MM"));
        }

        // Create execution table partitions for current month + next 2 months
        for (var i = 0; i < 3; i++)
        {
            var targetDate = now.AddMonths(i);
            await using var cmd = new NpgsqlCommand("SELECT create_execution_monthly_partitions($1::date)", connection);
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateOnly.FromDateTime(targetDate).ToDateTime(TimeOnly.MinValue) });
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogDebug("Ensured execution partitions exist for {Month}", targetDate.ToString("yyyy-MM"));
        }

        // Read retention settings (informational in V1)
        await LogRetentionSettingAsync(connection, "audit.partition_retention_months", "Audit log", ct);
        await LogRetentionSettingAsync(connection, "execution.partition_retention_months", "Execution", ct);

        _logger.LogInformation("Partition maintenance completed successfully");
    }

    private async Task LogRetentionSettingAsync(
        NpgsqlConnection connection,
        string settingKey,
        string label,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT value FROM system_settings WHERE key = $1",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter { Value = settingKey });
        var retentionValue = await cmd.ExecuteScalarAsync(ct);
        if (retentionValue is string retention)
        {
            _logger.LogInformation("{Label} partition retention policy: {Months} months (archive/drop deferred to V2)", label, retention);
        }
    }

    internal static TimeSpan GetDelayUntilNextRun(DateTime? now = null)
    {
        var current = now ?? DateTime.UtcNow;

        // Find next Monday 00:00 UTC
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)current.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
            daysUntilMonday = 7; // Always wait at least until next Monday

        var nextMonday = current.Date.AddDays(daysUntilMonday);
        return nextMonday - current;
    }
}
