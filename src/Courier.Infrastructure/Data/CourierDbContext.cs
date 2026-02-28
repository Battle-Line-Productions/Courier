using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Courier.Infrastructure.Data;

public class CourierDbContext : DbContext
{
    public CourierDbContext(DbContextOptions<CourierDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobStep> JobSteps => Set<JobStep>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<StepExecution> StepExecutions => Set<StepExecution>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<KnownHost> KnownHosts => Set<KnownHost>();
    public DbSet<PgpKey> PgpKeys => Set<PgpKey>();
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    public DbSet<JobSchedule> JobSchedules => Set<JobSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.CurrentVersion)
                .HasColumnName("current_version")
                .HasDefaultValue(1);

            entity.Property(e => e.IsEnabled)
                .HasColumnName("is_enabled")
                .HasDefaultValue(true);

            entity.Property(e => e.FailurePolicy)
                .HasColumnName("failure_policy")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.IsDeleted)
                .HasColumnName("is_deleted")
                .HasDefaultValue(false);

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name)
                .HasFilter("NOT is_deleted");

            entity.HasIndex(e => e.IsEnabled)
                .HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<JobStep>(entity =>
        {
            entity.ToTable("job_steps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.StepOrder).HasColumnName("step_order");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.TypeKey).HasColumnName("type_key").IsRequired();
            entity.Property(e => e.Configuration).HasColumnName("configuration").HasColumnType("jsonb");
            entity.Property(e => e.TimeoutSeconds).HasColumnName("timeout_seconds").HasDefaultValue(300);

            entity.HasOne(e => e.Job).WithMany(j => j.Steps).HasForeignKey(e => e.JobId);
            entity.HasIndex(e => new { e.JobId, e.StepOrder }).IsUnique();
        });

        modelBuilder.Entity<JobExecution>(entity =>
        {
            entity.ToTable("job_executions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.JobVersionNumber).HasColumnName("job_version_number").HasDefaultValue(1);
            entity.Property(e => e.TriggeredBy).HasColumnName("triggered_by").IsRequired();
            entity.Property(e => e.State).HasColumnName("state")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<JobExecutionState>(v, true));
            entity.Property(e => e.QueuedAt).HasColumnName("queued_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ContextSnapshot).HasColumnName("context_snapshot").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Job).WithMany(j => j.Executions).HasForeignKey(e => e.JobId);
        });

        modelBuilder.Entity<StepExecution>(entity =>
        {
            entity.ToTable("step_executions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobExecutionId).HasColumnName("job_execution_id");
            entity.Property(e => e.JobStepId).HasColumnName("job_step_id");
            entity.Property(e => e.StepOrder).HasColumnName("step_order");
            entity.Property(e => e.State).HasColumnName("state")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<StepExecutionState>(v, true));
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.BytesProcessed).HasColumnName("bytes_processed");
            entity.Property(e => e.OutputData).HasColumnName("output_data").HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ErrorStackTrace).HasColumnName("error_stack_trace");
            entity.Property(e => e.RetryAttempt).HasColumnName("retry_attempt");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.JobExecution).WithMany(je => je.StepExecutions).HasForeignKey(e => e.JobExecutionId);
            entity.HasOne(e => e.JobStep).WithMany().HasForeignKey(e => e.JobStepId);
        });

        modelBuilder.Entity<Connection>(entity =>
        {
            entity.ToTable("connections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Group).HasColumnName("group");
            entity.Property(e => e.Protocol).HasColumnName("protocol").IsRequired();
            entity.Property(e => e.Host).HasColumnName("host").IsRequired();
            entity.Property(e => e.Port).HasColumnName("port");
            entity.Property(e => e.AuthMethod).HasColumnName("auth_method").IsRequired();
            entity.Property(e => e.Username).HasColumnName("username").IsRequired();
            entity.Property(e => e.PasswordEncrypted).HasColumnName("password_encrypted");
            entity.Property(e => e.SshKeyId).HasColumnName("ssh_key_id");
            entity.Property(e => e.HostKeyPolicy).HasColumnName("host_key_policy").IsRequired();
            entity.Property(e => e.StoredHostFingerprint).HasColumnName("stored_host_fingerprint");
            entity.Property(e => e.SshAlgorithms).HasColumnName("ssh_algorithms").HasColumnType("jsonb");
            entity.Property(e => e.PassiveMode).HasColumnName("passive_mode").HasDefaultValue(true);
            entity.Property(e => e.TlsVersionFloor).HasColumnName("tls_version_floor");
            entity.Property(e => e.TlsCertPolicy).HasColumnName("tls_cert_policy").IsRequired();
            entity.Property(e => e.TlsPinnedThumbprint).HasColumnName("tls_pinned_thumbprint");
            entity.Property(e => e.ConnectTimeoutSec).HasColumnName("connect_timeout_sec").HasDefaultValue(30);
            entity.Property(e => e.OperationTimeoutSec).HasColumnName("operation_timeout_sec").HasDefaultValue(300);
            entity.Property(e => e.KeepaliveIntervalSec).HasColumnName("keepalive_interval_sec").HasDefaultValue(60);
            entity.Property(e => e.TransportRetries).HasColumnName("transport_retries").HasDefaultValue(2);
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.FipsOverride).HasColumnName("fips_override").HasDefaultValue(false);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Group).HasFilter("NOT is_deleted AND \"group\" IS NOT NULL");
            entity.HasIndex(e => e.Protocol).HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<KnownHost>(entity =>
        {
            entity.ToTable("known_hosts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id");
            entity.Property(e => e.Fingerprint).HasColumnName("fingerprint").IsRequired();
            entity.Property(e => e.KeyType).HasColumnName("key_type").IsRequired();
            entity.Property(e => e.FirstSeen).HasColumnName("first_seen");
            entity.Property(e => e.LastSeen).HasColumnName("last_seen");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by").IsRequired();

            entity.HasOne(e => e.Connection).WithMany(c => c.KnownHosts).HasForeignKey(e => e.ConnectionId);
            entity.HasIndex(e => new { e.ConnectionId, e.Fingerprint }).IsUnique();
        });

        modelBuilder.Entity<PgpKey>(entity =>
        {
            entity.ToTable("pgp_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Fingerprint).HasColumnName("fingerprint");
            entity.Property(e => e.ShortKeyId).HasColumnName("short_key_id");
            entity.Property(e => e.Algorithm).HasColumnName("algorithm").IsRequired();
            entity.Property(e => e.KeyType).HasColumnName("key_type").IsRequired();
            entity.Property(e => e.Purpose).HasColumnName("purpose");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.PublicKeyData).HasColumnName("public_key_data");
            entity.Property(e => e.PrivateKeyData).HasColumnName("private_key_data");
            entity.Property(e => e.PassphraseHash).HasColumnName("passphrase_hash");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.SuccessorKeyId).HasColumnName("successor_key_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Status).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Fingerprint).IsUnique().HasFilter("NOT is_deleted AND fingerprint IS NOT NULL");
        });

        modelBuilder.Entity<SshKey>(entity =>
        {
            entity.ToTable("ssh_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.KeyType).HasColumnName("key_type").IsRequired();
            entity.Property(e => e.PublicKeyData).HasColumnName("public_key_data");
            entity.Property(e => e.PrivateKeyData).HasColumnName("private_key_data");
            entity.Property(e => e.PassphraseHash).HasColumnName("passphrase_hash");
            entity.Property(e => e.Fingerprint).HasColumnName("fingerprint");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Status).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Fingerprint).IsUnique().HasFilter("NOT is_deleted AND fingerprint IS NOT NULL");
        });

        modelBuilder.Entity<JobSchedule>(entity =>
        {
            entity.ToTable("job_schedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ScheduleType).HasColumnName("schedule_type").IsRequired();
            entity.Property(e => e.CronExpression).HasColumnName("cron_expression");
            entity.Property(e => e.RunAt).HasColumnName("run_at");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.LastFiredAt).HasColumnName("last_fired_at");
            entity.Property(e => e.NextFireAt).HasColumnName("next_fire_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Job).WithMany(j => j.Schedules).HasForeignKey(e => e.JobId);
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => new { e.IsEnabled, e.ScheduleType });
        });
    }
}
