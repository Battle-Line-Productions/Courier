using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobDependencyService
{
    private readonly CourierDbContext _db;

    public JobDependencyService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<JobDependencyDto>>> ListDependenciesAsync(Guid jobId, CancellationToken ct = default)
    {
        var dependencies = await _db.JobDependencies
            .Include(d => d.UpstreamJob)
            .Where(d => d.DownstreamJobId == jobId)
            .OrderBy(d => d.UpstreamJob.Name)
            .Select(d => MapToDto(d))
            .ToListAsync(ct);

        return new ApiResponse<List<JobDependencyDto>> { Data = dependencies };
    }

    public async Task<ApiResponse<JobDependencyDto>> AddDependencyAsync(
        Guid downstreamJobId, Guid upstreamJobId, bool runOnFailure, CancellationToken ct = default)
    {
        // Self-dependency check
        if (downstreamJobId == upstreamJobId)
        {
            return new ApiResponse<JobDependencyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.SelfDependency, "A job cannot depend on itself.")
            };
        }

        // Validate both jobs exist
        var downstream = await _db.Jobs.FindAsync([downstreamJobId], ct);
        if (downstream is null)
        {
            return new ApiResponse<JobDependencyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job with id '{downstreamJobId}' not found.")
            };
        }

        var upstream = await _db.Jobs.FindAsync([upstreamJobId], ct);
        if (upstream is null)
        {
            return new ApiResponse<JobDependencyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Upstream job with id '{upstreamJobId}' not found.")
            };
        }

        // Duplicate check
        var exists = await _db.JobDependencies
            .AnyAsync(d => d.UpstreamJobId == upstreamJobId && d.DownstreamJobId == downstreamJobId, ct);

        if (exists)
        {
            return new ApiResponse<JobDependencyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateDependency, "This dependency already exists.")
            };
        }

        // Circular dependency check (BFS from upstream to see if we can reach downstream)
        if (await HasCircularDependencyAsync(upstreamJobId, downstreamJobId, ct))
        {
            return new ApiResponse<JobDependencyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.CircularDependency,
                    "Adding this dependency would create a circular dependency.")
            };
        }

        var dependency = new JobDependency
        {
            Id = Guid.CreateVersion7(),
            UpstreamJobId = upstreamJobId,
            DownstreamJobId = downstreamJobId,
            RunOnFailure = runOnFailure,
        };

        _db.JobDependencies.Add(dependency);
        await _db.SaveChangesAsync(ct);

        dependency.UpstreamJob = upstream;
        return new ApiResponse<JobDependencyDto> { Data = MapToDto(dependency) };
    }

    public async Task<ApiResponse> RemoveDependencyAsync(Guid jobId, Guid dependencyId, CancellationToken ct = default)
    {
        var dependency = await _db.JobDependencies
            .FirstOrDefaultAsync(d => d.Id == dependencyId && d.DownstreamJobId == jobId, ct);

        if (dependency is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.DependencyNotFound,
                    $"Dependency with id '{dependencyId}' not found for job '{jobId}'.")
            };
        }

        _db.JobDependencies.Remove(dependency);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse();
    }

    private async Task<bool> HasCircularDependencyAsync(Guid upstreamJobId, Guid downstreamJobId, CancellationToken ct)
    {
        // BFS: starting from upstreamJobId, traverse its own upstream dependencies
        // If we can reach downstreamJobId, adding this edge would create a cycle
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(upstreamJobId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == downstreamJobId)
                return true;

            if (!visited.Add(current))
                continue;

            var upstreams = await _db.JobDependencies
                .Where(d => d.DownstreamJobId == current)
                .Select(d => d.UpstreamJobId)
                .ToListAsync(ct);

            foreach (var up in upstreams)
                queue.Enqueue(up);
        }

        return false;
    }

    private static JobDependencyDto MapToDto(JobDependency d) => new()
    {
        Id = d.Id,
        UpstreamJobId = d.UpstreamJobId,
        UpstreamJobName = d.UpstreamJob?.Name ?? string.Empty,
        DownstreamJobId = d.DownstreamJobId,
        RunOnFailure = d.RunOnFailure,
    };
}
