using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Connections;

public class KnownHostService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public KnownHostService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<List<KnownHostDto>>> GetByConnectionIdAsync(Guid connectionId, CancellationToken ct = default)
    {
        var connectionExists = await _db.Connections.AnyAsync(c => c.Id == connectionId, ct);

        if (!connectionExists)
        {
            return new ApiResponse<List<KnownHostDto>>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{connectionId}' not found.")
            };
        }

        var knownHosts = await _db.KnownHosts
            .Where(kh => kh.ConnectionId == connectionId)
            .OrderByDescending(kh => kh.LastSeen)
            .Select(kh => MapToDto(kh))
            .ToListAsync(ct);

        return new ApiResponse<List<KnownHostDto>> { Data = knownHosts };
    }

    public async Task<ApiResponse<KnownHostDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var knownHost = await _db.KnownHosts.FirstOrDefaultAsync(kh => kh.Id == id, ct);

        if (knownHost is null)
        {
            return new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KnownHostNotFound, $"Known host with id '{id}' not found.")
            };
        }

        return new ApiResponse<KnownHostDto> { Data = MapToDto(knownHost) };
    }

    public async Task<ApiResponse<KnownHostDto>> CreateAsync(Guid connectionId, CreateKnownHostRequest request, CancellationToken ct = default)
    {
        var connectionExists = await _db.Connections.AnyAsync(c => c.Id == connectionId, ct);

        if (!connectionExists)
        {
            return new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{connectionId}' not found.")
            };
        }

        var duplicateExists = await _db.KnownHosts
            .AnyAsync(kh => kh.ConnectionId == connectionId && kh.Fingerprint == request.Fingerprint, ct);

        if (duplicateExists)
        {
            return new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateKnownHostFingerprint, $"A known host with fingerprint '{request.Fingerprint}' already exists for this connection.")
            };
        }

        var now = DateTime.UtcNow;
        var knownHost = new KnownHost
        {
            Id = Guid.CreateVersion7(),
            ConnectionId = connectionId,
            KeyType = request.KeyType,
            Fingerprint = request.Fingerprint,
            FirstSeen = now,
            LastSeen = now,
            ApprovedBy = string.Empty,
        };

        _db.KnownHosts.Add(knownHost);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.KnownHost, knownHost.Id, "Created", details: new { knownHost.Fingerprint, knownHost.ConnectionId }, ct: ct);

        return new ApiResponse<KnownHostDto> { Data = MapToDto(knownHost) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var knownHost = await _db.KnownHosts.FindAsync([id], ct);

        if (knownHost is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.KnownHostNotFound, $"Known host with id '{id}' not found.")
            };
        }

        _db.KnownHosts.Remove(knownHost);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.KnownHost, id, "Deleted", details: new { knownHost.Fingerprint, knownHost.ConnectionId }, ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<KnownHostDto>> ApproveAsync(Guid id, string approvedBy, CancellationToken ct = default)
    {
        var knownHost = await _db.KnownHosts.FindAsync([id], ct);

        if (knownHost is null)
        {
            return new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KnownHostNotFound, $"Known host with id '{id}' not found.")
            };
        }

        if (!string.IsNullOrEmpty(knownHost.ApprovedBy))
        {
            return new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KnownHostAlreadyApproved, $"Known host with id '{id}' is already approved.")
            };
        }

        knownHost.ApprovedBy = approvedBy;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.KnownHost, id, "Approved", details: new { knownHost.Fingerprint, approvedBy }, ct: ct);

        return new ApiResponse<KnownHostDto> { Data = MapToDto(knownHost) };
    }

    private static KnownHostDto MapToDto(KnownHost kh) => new()
    {
        Id = kh.Id,
        ConnectionId = kh.ConnectionId,
        KeyType = kh.KeyType,
        Fingerprint = kh.Fingerprint,
        IsApproved = !string.IsNullOrEmpty(kh.ApprovedBy),
        ApprovedBy = string.IsNullOrEmpty(kh.ApprovedBy) ? null : kh.ApprovedBy,
        FirstSeen = kh.FirstSeen,
        LastSeen = kh.LastSeen,
    };
}
