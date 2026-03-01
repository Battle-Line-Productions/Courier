using Courier.Domain.Common;
using Courier.Domain.Encryption;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Connections;

public class ConnectionService
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;

    public ConnectionService(CourierDbContext db, ICredentialEncryptor encryptor)
    {
        _db = db;
        _encryptor = encryptor;
    }

    public async Task<ApiResponse<ConnectionDto>> CreateAsync(CreateConnectionRequest request, CancellationToken ct = default)
    {
        var port = request.Port ?? GetDefaultPort(request.Protocol);

        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Group = request.Group,
            Protocol = request.Protocol,
            Host = request.Host,
            Port = port,
            AuthMethod = request.AuthMethod,
            Username = request.Username,
            PasswordEncrypted = request.Password is not null ? _encryptor.Encrypt(request.Password) : null,
            ClientSecretEncrypted = request.ClientSecret is not null ? _encryptor.Encrypt(request.ClientSecret) : null,
            Properties = request.Properties,
            SshKeyId = request.SshKeyId,
            HostKeyPolicy = request.HostKeyPolicy ?? "trust_on_first_use",
            SshAlgorithms = request.SshAlgorithms,
            PassiveMode = request.PassiveMode ?? true,
            TlsVersionFloor = request.TlsVersionFloor,
            TlsCertPolicy = request.TlsCertPolicy ?? "system_trust",
            TlsPinnedThumbprint = request.TlsPinnedThumbprint,
            ConnectTimeoutSec = request.ConnectTimeoutSec ?? 30,
            OperationTimeoutSec = request.OperationTimeoutSec ?? 300,
            KeepaliveIntervalSec = request.KeepaliveIntervalSec ?? 60,
            TransportRetries = request.TransportRetries ?? 2,
            Status = "active",
            FipsOverride = request.FipsOverride ?? false,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Connections.Add(connection);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<ConnectionDto> { Data = MapToDto(connection) };
    }

    public async Task<ApiResponse<ConnectionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (connection is null)
        {
            return new ApiResponse<ConnectionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{id}' not found.")
            };
        }

        return new ApiResponse<ConnectionDto> { Data = MapToDto(connection) };
    }

    public async Task<PagedApiResponse<ConnectionDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? protocol = null,
        string? group = null,
        string? status = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.Connections.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term) || c.Host.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(protocol))
            query = query.Where(c => c.Protocol == protocol);

        if (!string.IsNullOrWhiteSpace(group))
            query = query.Where(c => c.Group == group);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(ct);

        return new PagedApiResponse<ConnectionDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<ConnectionDto>> UpdateAsync(Guid id, UpdateConnectionRequest request, CancellationToken ct = default)
    {
        var connection = await _db.Connections.FindAsync([id], ct);

        if (connection is null)
        {
            return new ApiResponse<ConnectionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{id}' not found.")
            };
        }

        connection.Name = request.Name;
        connection.Group = request.Group;
        connection.Protocol = request.Protocol;
        connection.Host = request.Host;
        connection.Port = request.Port ?? GetDefaultPort(request.Protocol);
        connection.AuthMethod = request.AuthMethod;
        connection.Username = request.Username;
        connection.SshKeyId = request.SshKeyId;
        connection.Properties = request.Properties;
        connection.HostKeyPolicy = request.HostKeyPolicy ?? connection.HostKeyPolicy;
        connection.SshAlgorithms = request.SshAlgorithms;
        connection.PassiveMode = request.PassiveMode ?? connection.PassiveMode;
        connection.TlsVersionFloor = request.TlsVersionFloor;
        connection.TlsCertPolicy = request.TlsCertPolicy ?? connection.TlsCertPolicy;
        connection.TlsPinnedThumbprint = request.TlsPinnedThumbprint;
        connection.ConnectTimeoutSec = request.ConnectTimeoutSec ?? connection.ConnectTimeoutSec;
        connection.OperationTimeoutSec = request.OperationTimeoutSec ?? connection.OperationTimeoutSec;
        connection.KeepaliveIntervalSec = request.KeepaliveIntervalSec ?? connection.KeepaliveIntervalSec;
        connection.TransportRetries = request.TransportRetries ?? connection.TransportRetries;
        connection.Status = request.Status ?? connection.Status;
        connection.FipsOverride = request.FipsOverride ?? connection.FipsOverride;
        connection.Notes = request.Notes;

        // Password semantics: null = no change, "" = clear, non-empty = re-encrypt
        if (request.Password is not null)
        {
            connection.PasswordEncrypted = request.Password.Length > 0
                ? _encryptor.Encrypt(request.Password)
                : null;
        }

        // ClientSecret follows the same semantics as Password
        if (request.ClientSecret is not null)
        {
            connection.ClientSecretEncrypted = request.ClientSecret.Length > 0
                ? _encryptor.Encrypt(request.ClientSecret)
                : null;
        }

        connection.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse<ConnectionDto> { Data = MapToDto(connection) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await _db.Connections.FindAsync([id], ct);

        if (connection is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Connection with id '{id}' not found.")
            };
        }

        connection.IsDeleted = true;
        connection.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ApiResponse();
    }

    private static int GetDefaultPort(string protocol) => protocol switch
    {
        "sftp" => 22,
        "ftp" => 21,
        "ftps" => 990,
        "azure_function" => 443,
        _ => 22
    };

    private static ConnectionDto MapToDto(Connection c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Group = c.Group,
        Protocol = c.Protocol,
        Host = c.Host,
        Port = c.Port,
        AuthMethod = c.AuthMethod,
        Username = c.Username,
        HasPassword = c.PasswordEncrypted is not null,
        HasClientSecret = c.ClientSecretEncrypted is not null,
        Properties = c.Properties,
        SshKeyId = c.SshKeyId,
        HostKeyPolicy = c.HostKeyPolicy,
        StoredHostFingerprint = c.StoredHostFingerprint,
        SshAlgorithms = c.SshAlgorithms,
        PassiveMode = c.PassiveMode,
        TlsVersionFloor = c.TlsVersionFloor,
        TlsCertPolicy = c.TlsCertPolicy,
        TlsPinnedThumbprint = c.TlsPinnedThumbprint,
        ConnectTimeoutSec = c.ConnectTimeoutSec,
        OperationTimeoutSec = c.OperationTimeoutSec,
        KeepaliveIntervalSec = c.KeepaliveIntervalSec,
        TransportRetries = c.TransportRetries,
        Status = c.Status,
        FipsOverride = c.FipsOverride,
        Notes = c.Notes,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
