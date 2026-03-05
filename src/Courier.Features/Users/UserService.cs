using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Auth;
using Courier.Features.AuditLog;
using Courier.Features.Settings;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Users;

public class UserService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;

    public UserService(CourierDbContext db, AuditService audit, SettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    public async Task<PagedApiResponse<UserDto>> ListAsync(
        int page = 1, int pageSize = 25, string? search = null, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }

        query = query.OrderBy(u => u.Username);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => MapToDto(u))
            .ToListAsync(ct);

        return new PagedApiResponse<UserDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, $"User with id '{id}' not found.")
            };
        }

        return new ApiResponse<UserDto> { Data = MapToDto(user) };
    }

    public async Task<ApiResponse<UserDto>> CreateAsync(CreateUserRequest request, string performedBy, CancellationToken ct = default)
    {
        var duplicateExists = await _db.Users
            .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower(), ct);

        if (duplicateExists)
        {
            return new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.DuplicateUsername, $"A user with username '{request.Username}' already exists.")
            };
        }

        // Validate password strength
        var minLengthStr = await _settings.GetSettingAsync("auth.password_min_length", ct);
        var minLength = int.TryParse(minLengthStr, out var ml) ? ml : 8;

        if (request.Password.Length < minLength)
        {
            return new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.WeakPassword, $"Password must be at least {minLength} characters.")
            };
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, user.Id, "Created", details: new { user.Username, user.Role }, ct: ct);

        return new ApiResponse<UserDto> { Data = MapToDto(user) };
    }

    public async Task<ApiResponse<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, Guid performedById, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, $"User with id '{id}' not found.")
            };
        }

        // Guard: cannot demote the last admin
        if (user.Role == "admin" && request.Role != "admin")
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == "admin" && u.IsActive, ct);
            if (adminCount <= 1)
            {
                return new ApiResponse<UserDto>
                {
                    Error = ErrorMessages.Create(ErrorCodes.CannotDemoteLastAdmin, "Cannot demote the last admin user.")
                };
            }
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, id, "Updated", ct: ct);

        return new ApiResponse<UserDto> { Data = MapToDto(user) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, Guid performedById, CancellationToken ct = default)
    {
        if (id == performedById)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.CannotDeleteSelf, "You cannot delete your own account.")
            };
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, $"User with id '{id}' not found.")
            };
        }

        // Guard: cannot delete the last admin
        if (user.Role == "admin")
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == "admin" && u.IsActive, ct);
            if (adminCount <= 1)
            {
                return new ApiResponse
                {
                    Error = ErrorMessages.Create(ErrorCodes.CannotDemoteLastAdmin, "Cannot delete the last admin user.")
                };
            }
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse> ResetPasswordAsync(Guid id, NewPasswordRequest request, string performedBy, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.UserNotFound, $"User with id '{id}' not found.")
            };
        }

        var minLengthStr = await _settings.GetSettingAsync("auth.password_min_length", ct);
        var minLength = int.TryParse(minLengthStr, out var ml) ? ml : 8;

        if (request.Password.Length < minLength)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.WeakPassword, $"Password must be at least {minLength} characters.")
            };
        }

        user.PasswordHash = PasswordHasher.Hash(request.Password);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Revoke all refresh tokens
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == id && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.User, id, "PasswordReset", details: new { PerformedBy = performedBy }, ct: ct);

        return new ApiResponse();
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        DisplayName = u.DisplayName,
        Role = u.Role,
        IsActive = u.IsActive,
        IsSsoUser = u.IsSsoUser,
        LastLoginAt = u.LastLoginAt,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
    };
}
