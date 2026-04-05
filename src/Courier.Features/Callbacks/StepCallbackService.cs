using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Callbacks;

public class StepCallbackService
{
    private readonly CourierDbContext _db;

    public StepCallbackService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<(Guid CallbackId, string CallbackKey)> CreateAsync(
        int maxWaitSec, CancellationToken ct = default)
    {
        var callback = new StepCallback
        {
            Id = Guid.CreateVersion7(),
            CallbackKey = Guid.NewGuid().ToString("N"),
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(maxWaitSec),
        };

        _db.StepCallbacks.Add(callback);
        await _db.SaveChangesAsync(ct);

        return (callback.Id, callback.CallbackKey);
    }

    public async Task<StepCallback?> GetByIdAsync(Guid callbackId, CancellationToken ct = default)
    {
        return await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
    }

    public async Task<(bool Success, string? Error)> ProcessCallbackAsync(
        Guid callbackId, string key, CallbackRequest request, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);

        if (callback is null)
            return (false, "not_found");

        if (callback.CallbackKey != key)
            return (false, "unauthorized");

        if (callback.Status != "pending")
            return (false, "already_completed");

        if (DateTime.UtcNow > callback.ExpiresAt)
            return (false, "expired");

        callback.Status = request.Success ? "completed" : "failed";
        callback.ResultPayload = request.Output?.GetRawText();
        callback.ErrorMessage = request.ErrorMessage;
        callback.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task DeleteAsync(Guid callbackId, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
        if (callback is not null)
        {
            _db.StepCallbacks.Remove(callback);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkExpiredAsync(Guid callbackId, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
        if (callback is not null && callback.Status == "pending")
        {
            callback.Status = "failed";
            callback.ErrorMessage = "Callback expired — function did not respond in time.";
            callback.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
