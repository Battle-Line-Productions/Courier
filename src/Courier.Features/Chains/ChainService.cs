using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Tags;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Chains;

public class ChainService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public ChainService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<JobChainDto>> CreateAsync(CreateChainRequest request, CancellationToken ct = default)
    {
        var chain = new JobChain
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.JobChains.Add(chain);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, chain.Id, "Created", details: new { chain.Name }, ct: ct);

        return new ApiResponse<JobChainDto> { Data = MapToDto(chain) };
    }

    public async Task<ApiResponse<JobChainDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var chain = await _db.JobChains
            .Include(c => c.Members.OrderBy(m => m.ExecutionOrder))
                .ThenInclude(m => m.Job)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chain is null)
        {
            return new ApiResponse<JobChainDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{id}' not found.")
            };
        }

        var tags = await TagHelper.GetTagsForEntityAsync(_db, "job_chain", chain.Id, ct);
        return new ApiResponse<JobChainDto> { Data = MapToDto(chain, tags) };
    }

    public async Task<PagedApiResponse<JobChainDto>> ListAsync(int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.JobChains
            .Include(c => c.Members.OrderBy(m => m.ExecutionOrder))
                .ThenInclude(m => m.Job)
            .OrderByDescending(c => c.CreatedAt);

        var totalCount = await _db.JobChains.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(c => MapToDto(c)).ToList();
        var entityIds = dtos.Select(d => d.Id).ToList();
        var tagMap = await TagHelper.GetTagsForEntitiesAsync(_db, "job_chain", entityIds, ct);
        dtos = dtos.Select(d => d with { Tags = tagMap.GetValueOrDefault(d.Id, []) }).ToList();

        return new PagedApiResponse<JobChainDto>
        {
            Data = dtos,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<JobChainDto>> UpdateAsync(Guid id, string name, string? description, CancellationToken ct = default)
    {
        var chain = await _db.JobChains.FindAsync([id], ct);

        if (chain is null)
        {
            return new ApiResponse<JobChainDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{id}' not found.")
            };
        }

        chain.Name = name;
        chain.Description = description;
        chain.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, id, "Updated", ct: ct);

        return new ApiResponse<JobChainDto> { Data = MapToDto(chain) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var chain = await _db.JobChains.FindAsync([id], ct);

        if (chain is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{id}' not found.")
            };
        }

        chain.IsDeleted = true;
        chain.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<JobChainDto>> SetEnabledAsync(Guid id, bool isEnabled, CancellationToken ct = default)
    {
        var chain = await _db.JobChains.FindAsync([id], ct);

        if (chain is null)
        {
            return new ApiResponse<JobChainDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{id}' not found.")
            };
        }

        chain.IsEnabled = isEnabled;
        chain.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<JobChainDto> { Data = MapToDto(chain) };
    }

    public async Task<ApiResponse<List<JobChainMemberDto>>> ReplaceMembersAsync(
        Guid chainId,
        List<ChainMemberInput> inputs,
        CancellationToken ct = default)
    {
        var chain = await _db.JobChains
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == chainId, ct);

        if (chain is null)
        {
            return new ApiResponse<List<JobChainMemberDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain with id '{chainId}' not found.")
            };
        }

        // Validate all referenced jobs exist
        var jobIds = inputs.Select(i => i.JobId).Distinct().ToList();
        var existingJobs = await _db.Jobs
            .Where(j => jobIds.Contains(j.Id))
            .Select(j => new { j.Id, j.Name })
            .ToListAsync(ct);

        var existingJobMap = existingJobs.ToDictionary(j => j.Id, j => j.Name);
        var missingJobs = jobIds.Where(id => !existingJobMap.ContainsKey(id)).ToList();
        if (missingJobs.Count > 0)
        {
            return new ApiResponse<List<JobChainMemberDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ChainMemberJobNotFound,
                    $"Jobs not found: {string.Join(", ", missingJobs)}")
            };
        }

        // Create new members with generated IDs
        var newMembers = new List<JobChainMember>();
        foreach (var input in inputs)
        {
            newMembers.Add(new JobChainMember
            {
                Id = Guid.CreateVersion7(),
                ChainId = chainId,
                JobId = input.JobId,
                ExecutionOrder = input.ExecutionOrder,
                RunOnUpstreamFailure = input.RunOnUpstreamFailure,
            });
        }

        // Resolve DependsOnMemberIndex → DependsOnMemberId
        for (var i = 0; i < inputs.Count; i++)
        {
            if (inputs[i].DependsOnMemberIndex is { } depIdx)
            {
                if (depIdx < 0 || depIdx >= newMembers.Count || depIdx == i)
                {
                    return new ApiResponse<List<JobChainMemberDto>>
                    {
                        Error = ErrorMessages.Create(ErrorCodes.CircularDependency,
                            $"Invalid dependency index {depIdx} for member at index {i}.")
                    };
                }
                newMembers[i].DependsOnMemberId = newMembers[depIdx].Id;
            }
        }

        // Validate no circular dependencies (topological sort)
        var circularError = ValidateNoCycles(newMembers);
        if (circularError is not null)
        {
            return new ApiResponse<List<JobChainMemberDto>>
            {
                Error = circularError
            };
        }

        // Remove existing members and add new ones
        _db.JobChainMembers.RemoveRange(chain.Members);
        _db.JobChainMembers.AddRange(newMembers);
        chain.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, chainId, "MembersReplaced",
            details: new { MemberCount = newMembers.Count }, ct: ct);

        var dtos = newMembers.Select(m => new JobChainMemberDto
        {
            Id = m.Id,
            JobId = m.JobId,
            JobName = existingJobMap[m.JobId],
            ExecutionOrder = m.ExecutionOrder,
            DependsOnMemberId = m.DependsOnMemberId,
            RunOnUpstreamFailure = m.RunOnUpstreamFailure,
        }).ToList();

        return new ApiResponse<List<JobChainMemberDto>> { Data = dtos };
    }

    private static ApiError? ValidateNoCycles(List<JobChainMember> members)
    {
        var memberById = members.ToDictionary(m => m.Id);
        var visited = new HashSet<Guid>();
        var inStack = new HashSet<Guid>();

        foreach (var member in members)
        {
            if (HasCycle(member.Id, memberById, visited, inStack))
            {
                return ErrorMessages.Create(ErrorCodes.CircularDependency,
                    "Circular dependency detected among chain members.");
            }
        }

        return null;
    }

    private static bool HasCycle(
        Guid memberId,
        Dictionary<Guid, JobChainMember> memberById,
        HashSet<Guid> visited,
        HashSet<Guid> inStack)
    {
        if (inStack.Contains(memberId))
            return true;

        if (visited.Contains(memberId))
            return false;

        visited.Add(memberId);
        inStack.Add(memberId);

        var member = memberById[memberId];
        if (member.DependsOnMemberId is { } depId && memberById.ContainsKey(depId))
        {
            if (HasCycle(depId, memberById, visited, inStack))
                return true;
        }

        inStack.Remove(memberId);
        return false;
    }

    private static JobChainDto MapToDto(JobChain chain, List<TagSummaryDto>? tags = null) => new()
    {
        Id = chain.Id,
        Name = chain.Name,
        Description = chain.Description,
        IsEnabled = chain.IsEnabled,
        Members = chain.Members
            .OrderBy(m => m.ExecutionOrder)
            .Select(m => new JobChainMemberDto
            {
                Id = m.Id,
                JobId = m.JobId,
                JobName = m.Job?.Name ?? string.Empty,
                ExecutionOrder = m.ExecutionOrder,
                DependsOnMemberId = m.DependsOnMemberId,
                RunOnUpstreamFailure = m.RunOnUpstreamFailure,
            })
            .ToList(),
        Tags = tags ?? [],
        CreatedAt = chain.CreatedAt,
        UpdatedAt = chain.UpdatedAt,
    };
}
