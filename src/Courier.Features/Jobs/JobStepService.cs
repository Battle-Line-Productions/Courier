using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobStepService
{
    private readonly CourierDbContext _db;

    public JobStepService(CourierDbContext db) => _db = db;

    public async Task<ApiResponse<JobStepDto>> AddStepAsync(Guid jobId, AddJobStepRequest request, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobStepDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var step = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StepOrder = request.StepOrder,
            Name = request.Name,
            TypeKey = request.TypeKey,
            Configuration = request.Configuration,
            TimeoutSeconds = request.TimeoutSeconds,
        };

        _db.JobSteps.Add(step);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<JobStepDto> { Data = MapToDto(step) };
    }

    public async Task<ApiResponse<List<JobStepDto>>> ListStepsAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<List<JobStepDto>> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var steps = await _db.JobSteps
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.StepOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return new ApiResponse<List<JobStepDto>> { Data = steps };
    }

    private static JobStepDto MapToDto(JobStep s) => new()
    {
        Id = s.Id,
        JobId = s.JobId,
        StepOrder = s.StepOrder,
        Name = s.Name,
        TypeKey = s.TypeKey,
        Configuration = s.Configuration,
        TimeoutSeconds = s.TimeoutSeconds,
    };
}
