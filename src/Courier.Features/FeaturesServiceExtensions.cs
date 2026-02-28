using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Connections;
using Courier.Features.Engine;
using Courier.Features.Engine.Steps;
using Courier.Features.Filesystem;
using Courier.Features.Jobs;
using Courier.Infrastructure.Encryption;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Courier.Features;

public static class FeaturesServiceExtensions
{
    public static IServiceCollection AddCourierFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<JobService>();
        services.AddScoped<JobStepService>();
        services.AddScoped<ExecutionService>();
        services.AddScoped<FilesystemService>();
        services.AddScoped<JobEngine>();
        services.AddSingleton<IJobStep, FileCopyStep>();
        services.AddSingleton<IJobStep, FileMoveStep>();
        services.AddSingleton<StepTypeRegistry>();
        services.AddValidatorsFromAssemblyContaining<CreateJobValidator>();

        // Connections
        services.AddScoped<ConnectionService>();

        // Encryption
        services.Configure<EncryptionSettings>(configuration.GetSection("Encryption"));
        services.AddSingleton<ICredentialEncryptor, AesGcmCredentialEncryptor>();

        return services;
    }
}
