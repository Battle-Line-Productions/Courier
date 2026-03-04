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

        // Create partitions for current month + next 2 months
        for (var i = 0; i < 3; i++)
        {
            var targetDate = now.AddMonths(i);
            await using var cmd = new NpgsqlCommand("SELECT create_monthly_partitions($1::date)", connection);
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateOnly.FromDateTime(targetDate).ToDateTime(TimeOnly.MinValue) });
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogDebug("Ensured partition exists for {Month}", targetDate.ToString("yyyy-MM"));
        }

        // Read retention setting (informational in V1)
        await using var retentionCmd = new NpgsqlCommand(
            "SELECT value FROM system_settings WHERE key = 'audit.partition_retention_months'",
            connection);
        var retentionValue = await retentionCmd.ExecuteScalarAsync(ct);
        if (retentionValue is string retention)
        {
            _logger.LogInformation("Partition retention policy: {Months} months (archive/drop deferred to V2)", retention);
        }

        _logger.LogInformation("Partition maintenance completed successfully");
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
