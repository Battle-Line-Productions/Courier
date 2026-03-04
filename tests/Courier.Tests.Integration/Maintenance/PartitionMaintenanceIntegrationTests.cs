using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Courier.Tests.Integration.Maintenance;

public class PartitionMaintenanceIntegrationTests : IClassFixture<CourierApiFactory>
{
    private readonly HttpClient _client;
    private readonly CourierApiFactory _factory;

    public PartitionMaintenanceIntegrationTests(CourierApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthPartitions_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/partitions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateMonthlyPartitions_IsIdempotent()
    {
        // Arrange
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        // Act — call twice for current month, should not throw
        await using var cmd1 = new NpgsqlCommand("SELECT create_monthly_partitions($1::date)", connection);
        cmd1.Parameters.AddWithValue(DateTime.UtcNow.Date);
        await cmd1.ExecuteNonQueryAsync();

        await using var cmd2 = new NpgsqlCommand("SELECT create_monthly_partitions($1::date)", connection);
        cmd2.Parameters.AddWithValue(DateTime.UtcNow.Date);
        await cmd2.ExecuteNonQueryAsync();

        // Assert — no exception means idempotent
    }

    [Fact]
    public async Task CreateMonthlyPartitions_CreatesFuturePartition()
    {
        // Arrange
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var futureDate = DateTime.UtcNow.AddMonths(6);
        var expectedPartition = $"audit_log_entries_{futureDate:yyyy_MM}";

        // Act
        await using var cmd = new NpgsqlCommand("SELECT create_monthly_partitions($1::date)", connection);
        cmd.Parameters.AddWithValue(futureDate.Date);
        await cmd.ExecuteNonQueryAsync();

        // Assert
        await using var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'public' AND tablename = $1",
            connection);
        checkCmd.Parameters.AddWithValue(expectedPartition);
        var count = (long)(await checkCmd.ExecuteScalarAsync())!;
        count.ShouldBe(1);
    }

    private NpgsqlConnection CreateConnection()
    {
        // Access the connection string from the factory's test container
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Courier.Infrastructure.Data.CourierDbContext>();
        var connectionString = db.Database.GetConnectionString();
        return new NpgsqlConnection(connectionString);
    }
}
