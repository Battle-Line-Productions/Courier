using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Courier.Migrations;

/// <summary>
/// Worker-only hosted service that validates the database schema version on startup.
/// Refuses to start if the schema is behind the expected minimum migration.
/// </summary>
public class SchemaVersionValidator : IHostedService
{
    /// <summary>
    /// The minimum migration script that must be applied for this Worker version to operate.
    /// </summary>
    public const string ExpectedMinimumMigration = "0003_quartz_scheduler.sql";

    private readonly string _connectionString;
    private readonly ILogger<SchemaVersionValidator> _logger;

    public SchemaVersionValidator(IConfiguration configuration, ILogger<SchemaVersionValidator> logger)
    {
        _connectionString = configuration.GetConnectionString("CourierDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CourierDb is not configured.");
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating database schema version...");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Check if the schemaversions table exists
        await using var checkCmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schemaversions')", conn);
        var tableExists = (bool)(await checkCmd.ExecuteScalarAsync(cancellationToken))!;

        if (!tableExists)
        {
            _logger.LogCritical(
                "Schema versions table does not exist. The API host must run migrations before the Worker can start. " +
                "Deploy the API host first.");
            throw new InvalidOperationException(
                "Database schema is not initialized. Deploy the API host first to run migrations.");
        }

        // Check if the expected migration has been applied
        await using var queryCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM schemaversions WHERE scriptname LIKE @name", conn);
        queryCmd.Parameters.AddWithValue("name", $"%{ExpectedMinimumMigration}");
        var count = (long)(await queryCmd.ExecuteScalarAsync(cancellationToken))!;

        if (count == 0)
        {
            _logger.LogCritical(
                "Required migration '{Migration}' has not been applied. " +
                "The database schema is behind the Worker's expected version. Deploy the API host first.",
                ExpectedMinimumMigration);
            throw new InvalidOperationException(
                $"Required migration '{ExpectedMinimumMigration}' not found. Deploy the API host first.");
        }

        _logger.LogInformation("Schema version validated. Required migration '{Migration}' is present.",
            ExpectedMinimumMigration);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
