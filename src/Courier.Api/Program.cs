using System.Text;
using System.Threading.RateLimiting;
using Courier.Api.Middleware;
using Courier.Features;
using Courier.Features.Auth;
using Courier.Infrastructure.Data;
using Courier.Migrations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
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

// JWT Authentication
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (!string.IsNullOrEmpty(jwtSecret))
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "courier",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "courier-api",
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });
}

builder.Services.AddAuthorization();

// CORS — allow frontend origin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Controllers — discover from Features assembly
builder.Services.AddControllers()
    .AddApplicationPart(typeof(FeaturesServiceExtensions).Assembly);

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }
        else
        {
            context.HttpContext.Response.Headers.RetryAfter = "15";
        }
        return ValueTask.CompletedTask;
    };

    // Auth endpoints: configurable per-IP sliding window (default 120/min)
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 500);
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            }));

    // General API: configurable per-IP sliding window (default 2000/min), excludes auth endpoints
    var generalPermitLimit = builder.Configuration.GetValue("RateLimiting:GeneralPermitsPerMinute", 2000);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter<string>("auth-bypass");

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = generalPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0,
            });
    });
});

// Swagger (Development only configured below)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Exception middleware
builder.Services.AddSingleton<ApiExceptionMiddleware>();

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();

// Security headers
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<SetupGuardMiddleware>();

app.UseSerilogRequestLogging();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Make the Program class accessible for integration tests
namespace Courier.Api
{
    public partial class Program;
}
