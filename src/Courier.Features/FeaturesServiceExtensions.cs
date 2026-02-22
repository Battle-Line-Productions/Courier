using Courier.Domain.Engine;
using Courier.Features.Engine;
using Courier.Features.Engine.Steps;
using Courier.Features.Jobs;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Courier.Features;

public static class FeaturesServiceExtensions
{
    public static IServiceCollection AddCourierFeatures(this IServiceCollection services)
    {
        services.AddScoped<JobService>();
        services.AddScoped<JobStepService>();
        services.AddScoped<ExecutionService>();
        services.AddScoped<JobEngine>();
        services.AddSingleton<IJobStep, FileCopyStep>();
        services.AddSingleton<IJobStep, FileMoveStep>();
        services.AddSingleton<StepTypeRegistry>();
        services.AddValidatorsFromAssemblyContaining<CreateJobValidator>();
        return services;
    }
}
