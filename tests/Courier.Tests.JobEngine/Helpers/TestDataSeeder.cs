using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Domain.Encryption;
using Courier.Infrastructure.Data;

namespace Courier.Tests.JobEngine.Helpers;

public static class TestDataSeeder
{
    public static async Task<(Guid jobId, Guid executionId)> SeedJob(
        CourierDbContext db,
        string name = "test-job",
        string? failurePolicy = null)
    {
        var jobId = Guid.CreateVersion7();
        var executionId = Guid.CreateVersion7();

        var job = new Job
        {
            Id = jobId,
            Name = name,
            FailurePolicy = failurePolicy ?? """{"type":"stop","max_retries":0}""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var execution = new JobExecution
        {
            Id = executionId,
            JobId = jobId,
            State = JobExecutionState.Running,
            TriggeredBy = "test",
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        db.Jobs.Add(job);
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        return (jobId, executionId);
    }

    public static async Task<Guid> AddStep(
        CourierDbContext db,
        Guid jobId,
        int stepOrder,
        string typeKey,
        string name,
        object config,
        int timeoutSeconds = 300)
    {
        var stepId = Guid.CreateVersion7();
        var step = new JobStep
        {
            Id = stepId,
            JobId = jobId,
            StepOrder = stepOrder,
            Name = name,
            TypeKey = typeKey,
            Configuration = JsonSerializer.Serialize(config),
            TimeoutSeconds = timeoutSeconds,
        };

        db.JobSteps.Add(step);
        await db.SaveChangesAsync();
        return stepId;
    }

    public static async Task<Guid> SeedSftpConnection(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        string host,
        int port,
        string username,
        string password)
    {
        var id = Guid.CreateVersion7();
        var connection = new Connection
        {
            Id = id,
            Name = $"test-sftp-{id:N}",
            Protocol = "sftp",
            Host = host,
            Port = port,
            AuthMethod = "password",
            Username = username,
            PasswordEncrypted = encryptor.Encrypt(password),
            HostKeyPolicy = "always_trust",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Connections.Add(connection);
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedFtpConnection(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        string host,
        int port,
        string username,
        string password,
        bool ftps = false)
    {
        var id = Guid.CreateVersion7();
        var connection = new Connection
        {
            Id = id,
            Name = $"test-ftp-{id:N}",
            Protocol = ftps ? "ftps" : "ftp",
            Host = host,
            Port = port,
            AuthMethod = "password",
            Username = username,
            PasswordEncrypted = encryptor.Encrypt(password),
            TlsCertPolicy = ftps ? "insecure" : "system_trust",
            PassiveMode = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Connections.Add(connection);
        await db.SaveChangesAsync();
        return id;
    }
}
