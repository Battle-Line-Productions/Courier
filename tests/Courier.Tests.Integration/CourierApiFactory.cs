using System.Security.Claims;
using System.Text.Encodings.Web;
using Courier.Infrastructure.Data;
using Courier.Migrations;
using DbUp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Courier.Tests.Integration;

public class CourierApiFactory : WebApplicationFactory<Courier.Api.Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Run DbUp migrations against the test container
        var connectionString = _postgres.GetConnectionString();
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(MigrationMarker).Assembly)
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"Test migration failed: {result.Error.Message}", result.Error);

        // Mark setup as completed so SetupGuardMiddleware doesn't block requests
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE system_settings SET value = 'true' WHERE key = 'auth.setup_completed'";
        await cmd.ExecuteNonQueryAsync();

        // Invalidate the static cache in SetupGuardMiddleware
        Courier.Features.Auth.SetupGuardMiddleware.InvalidateCache();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Use a low auth rate limit so integration tests can trigger 429 with few requests
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "10");

        builder.ConfigureServices(services =>
        {
            // Remove the MigrationRunner so it doesn't run again
            services.RemoveAll<IHostedService>();

            // Remove all Aspire-registered EF Core services (pool, options, context)
            var descriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("CourierDbContext") == true)
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContext<CourierDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Add test authentication that auto-authenticates as admin
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

            // Override default auth scheme so Test takes priority over JwtBearer
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

/// <summary>
/// Authentication handler that auto-authenticates all requests as an admin user for integration tests.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            new Claim(ClaimTypes.Name, "testadmin"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("name", "Test Admin"),
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
