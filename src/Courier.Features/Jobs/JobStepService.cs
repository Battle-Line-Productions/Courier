using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobStepService
{
    private static readonly HashSet<string> ReservedAliases =
        new(StringComparer.OrdinalIgnoreCase) { "job", "loop" };

    private readonly CourierDbContext _db;

    public JobStepService(CourierDbContext db) => _db = db;

    public async Task<ApiResponse<JobStepDto>> AddStepAsync(Guid jobId, AddJobStepRequest request, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobStepDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        if (!string.IsNullOrEmpty(request.Alias))
        {
            if (ReservedAliases.Contains(request.Alias))
                return new ApiResponse<JobStepDto> { Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, $"Alias '{request.Alias}' is reserved and cannot be used as a step alias.") };

            var aliasExists = await _db.JobSteps.AnyAsync(
                s => s.JobId == jobId && s.Alias == request.Alias, ct);
            if (aliasExists)
                return new ApiResponse<JobStepDto> { Error = ErrorMessages.Create(ErrorCodes.DuplicateResource, $"Alias '{request.Alias}' is already used by another step in this job.") };
        }

        var step = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StepOrder = request.StepOrder,
            Name = request.Name,
            TypeKey = request.TypeKey,
            Configuration = request.Configuration,
            TimeoutSeconds = request.TimeoutSeconds,
            Alias = request.Alias,
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

    public async Task<ApiResponse<List<JobStepDto>>> ReplaceStepsAsync(Guid jobId, List<StepInput> steps, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([jobId], ct);

        if (job is null)
            return new ApiResponse<List<JobStepDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.")
            };

        var aliases = steps.Where(s => !string.IsNullOrEmpty(s.Alias)).Select(s => s.Alias!).ToList();
        if (aliases.Count != aliases.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return new ApiResponse<List<JobStepDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Step aliases must be unique within a job.")
            };

        var reservedAlias = aliases.FirstOrDefault(a => ReservedAliases.Contains(a));
        if (reservedAlias is not null)
            return new ApiResponse<List<JobStepDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, $"Alias '{reservedAlias}' is reserved and cannot be used as a step alias.")
            };

        var existingSteps = await _db.JobSteps
            .Where(s => s.JobId == jobId)
            .ToListAsync(ct);
        _db.JobSteps.RemoveRange(existingSteps);

        var newSteps = steps.Select(s => new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Name = s.Name,
            TypeKey = s.TypeKey,
            StepOrder = s.StepOrder,
            Configuration = s.Configuration,
            TimeoutSeconds = s.TimeoutSeconds,
            Alias = s.Alias,
        }).ToList();

        _db.JobSteps.AddRange(newSteps);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<List<JobStepDto>>
        {
            Data = newSteps.OrderBy(s => s.StepOrder).Select(MapToDto).ToList()
        };
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
        Alias = s.Alias,
    };
}
