using Courier.Api.Middleware;
using Courier.Features;
using Courier.Infrastructure.Data;
using Courier.Migrations;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();

    var seqUrl = context.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
        config.WriteTo.Seq(seqUrl);
});

// EF Core via Aspire integration (auto-wires ConnectionStrings:CourierDb)
builder.AddNpgsqlDbContext<CourierDbContext>("CourierDb");

// DbUp migrations (API only)
builder.Services.AddHostedService<MigrationRunner>();

// Features (Jobs, validators, etc.)
builder.Services.AddCourierFeatures(builder.Configuration);

// CORS — allow frontend origin in development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Controllers — discover from Features assembly
builder.Services.AddControllers()
    .AddApplicationPart(typeof(FeaturesServiceExtensions).Assembly);

// Swagger (Development only configured below)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Exception middleware
builder.Services.AddSingleton<ApiExceptionMiddleware>();

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Make the Program class accessible for integration tests
namespace Courier.Api
{
    public partial class Program;
}
