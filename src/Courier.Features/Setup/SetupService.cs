using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Setup;

public class SetupService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public SetupService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<SetupStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "auth.setup_completed", ct);

        return new ApiResponse<SetupStatusDto>
        {
            Data = new SetupStatusDto
            {
                IsCompleted = setting?.Value == "true"
            }
        };
    }

    public async Task<ApiResponse<UserProfileDto>> InitializeAsync(InitializeSetupRequest request, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "auth.setup_completed", ct);

        if (setting?.Value == "true")
        {
            return new ApiResponse<UserProfileDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.SetupAlreadyCompleted, "Initial setup has already been completed.")
            };
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = "admin",
            IsActive = true,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);

        if (setting is not null)
        {
            setting.Value = "true";
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = user.Username;
        }

        await _db.SaveChangesAsync(ct);

        SetupGuardMiddleware.InvalidateCache();

        try
        {
            await _audit.LogAsync(AuditableEntityType.User, user.Id, "Created", details: new { Role = "admin", SetupWizard = true }, ct: ct);
        }
        catch
        {
            // Audit logging should not fail setup
        }

        return new ApiResponse<UserProfileDto>
        {
            Data = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
            }
        };
    }
}
