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
    public DbSet<FileMonitor> FileMonitors => Set<FileMonitor>();
    public DbSet<MonitorJobBinding> MonitorJobBindings => Set<MonitorJobBinding>();
    public DbSet<MonitorFileLog> MonitorFileLogs => Set<MonitorFileLog>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntityTag> EntityTags => Set<EntityTag>();
    public DbSet<JobChain> JobChains => Set<JobChain>();
    public DbSet<JobChainMember> JobChainMembers => Set<JobChainMember>();
    public DbSet<ChainExecution> ChainExecutions => Set<ChainExecution>();
    public DbSet<JobDependency> JobDependencies => Set<JobDependency>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
    public DbSet<ChainSchedule> ChainSchedules => Set<ChainSchedule>();
    public DbSet<JobVersion> JobVersions => Set<JobVersion>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DomainEvent> DomainEvents => Set<DomainEvent>();
    public DbSet<KeyShareLink> KeyShareLinks => Set<KeyShareLink>();
    public DbSet<SsoUserLink> SsoUserLinks => Set<SsoUserLink>();
    public DbSet<StepCallback> StepCallbacks => Set<StepCallback>();

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
            entity.Property(e => e.Alias).HasColumnName("alias");

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
            entity.Property(e => e.PausedAt).HasColumnName("paused_at");
            entity.Property(e => e.PausedBy).HasColumnName("paused_by");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(e => e.CancelReason).HasColumnName("cancel_reason");
            entity.Property(e => e.RequestedState).HasColumnName("requested_state");
            entity.Property(e => e.ContextSnapshot).HasColumnName("context_snapshot").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.Property(e => e.RetryAttempt).HasColumnName("retry_attempt");
            entity.Property(e => e.ChainExecutionId).HasColumnName("chain_execution_id");

            entity.HasOne(e => e.Job).WithMany(j => j.Executions).HasForeignKey(e => e.JobId);
            entity.HasOne(e => e.ChainExecution).WithMany(ce => ce.JobExecutions).HasForeignKey(e => e.ChainExecutionId);
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
            entity.Property(e => e.IterationIndex).HasColumnName("iteration_index");
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
            entity.Property(e => e.ClientSecretEncrypted).HasColumnName("client_secret_encrypted");
            entity.Property(e => e.Properties).HasColumnName("properties").HasColumnType("jsonb");
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

        modelBuilder.Entity<FileMonitor>(entity =>
        {
            entity.ToTable("file_monitors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.WatchTarget).HasColumnName("watch_target").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.TriggerEvents).HasColumnName("trigger_events");
            entity.Property(e => e.FilePatterns).HasColumnName("file_patterns").HasColumnType("jsonb");
            entity.Property(e => e.PollingIntervalSec).HasColumnName("polling_interval_sec").HasDefaultValue(60);
            entity.Property(e => e.StabilityWindowSec).HasColumnName("stability_window_sec").HasDefaultValue(0);
            entity.Property(e => e.BatchMode).HasColumnName("batch_mode").HasDefaultValue(false);
            entity.Property(e => e.MaxConsecutiveFailures).HasColumnName("max_consecutive_failures").HasDefaultValue(5);
            entity.Property(e => e.ConsecutiveFailureCount).HasColumnName("consecutive_failure_count").HasDefaultValue(0);
            entity.Property(e => e.State).HasColumnName("state").IsRequired();
            entity.Property(e => e.LastPolledAt).HasColumnName("last_polled_at");
            entity.Property(e => e.LastPollDurationMs).HasColumnName("last_poll_duration_ms");
            entity.Property(e => e.LastPollFileCount).HasColumnName("last_poll_file_count");
            entity.Property(e => e.LastOverflowAt).HasColumnName("last_overflow_at");
            entity.Property(e => e.OverflowCount24h).HasColumnName("overflow_count_24h").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.State).HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<MonitorJobBinding>(entity =>
        {
            entity.ToTable("monitor_job_bindings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MonitorId).HasColumnName("monitor_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");

            entity.HasOne(e => e.Monitor).WithMany(m => m.Bindings).HasForeignKey(e => e.MonitorId);
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId);
            entity.HasIndex(e => new { e.MonitorId, e.JobId }).IsUnique();
            entity.HasIndex(e => e.JobId);
        });

        modelBuilder.Entity<MonitorFileLog>(entity =>
        {
            entity.ToTable("monitor_file_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MonitorId).HasColumnName("monitor_id");
            entity.Property(e => e.FilePath).HasColumnName("file_path").IsRequired();
            entity.Property(e => e.FileSize).HasColumnName("file_size").HasDefaultValue(0L);
            entity.Property(e => e.FileHash).HasColumnName("file_hash");
            entity.Property(e => e.LastModified).HasColumnName("last_modified");
            entity.Property(e => e.TriggeredAt).HasColumnName("triggered_at");
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id");

            entity.HasOne(e => e.Monitor).WithMany().HasForeignKey(e => e.MonitorId);
            entity.HasIndex(e => e.MonitorId);
            entity.HasIndex(e => new { e.MonitorId, e.FilePath });
            entity.HasIndex(e => e.TriggeredAt).IsDescending();
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_log_entries");
            entity.HasKey(e => new { e.Id, e.PerformedAt });
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.Operation).HasColumnName("operation").IsRequired();
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by").IsRequired();
            entity.Property(e => e.PerformedAt).HasColumnName("performed_at");
            entity.Property(e => e.Details).HasColumnName("details").HasColumnType("jsonb");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Color).HasColumnName("color");
            entity.Property(e => e.Category).HasColumnName("category");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Category).HasFilter("NOT is_deleted AND category IS NOT NULL");
        });

        modelBuilder.Entity<EntityTag>(entity =>
        {
            entity.ToTable("entity_tags");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");

            entity.HasOne(e => e.Tag).WithMany().HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TagId, e.EntityType, e.EntityId }).IsUnique();
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<JobChain>(entity =>
        {
            entity.ToTable("job_chains");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<ChainSchedule>(entity =>
        {
            entity.ToTable("chain_schedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChainId).HasColumnName("chain_id");
            entity.Property(e => e.ScheduleType).HasColumnName("schedule_type").IsRequired();
            entity.Property(e => e.CronExpression).HasColumnName("cron_expression");
            entity.Property(e => e.RunAt).HasColumnName("run_at");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.LastFiredAt).HasColumnName("last_fired_at");
            entity.Property(e => e.NextFireAt).HasColumnName("next_fire_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(e => e.Chain).WithMany(c => c.Schedules).HasForeignKey(e => e.ChainId);
            entity.HasIndex(e => e.ChainId);
            entity.HasIndex(e => new { e.IsEnabled, e.ScheduleType });
        });

        modelBuilder.Entity<JobChainMember>(entity =>
        {
            entity.ToTable("job_chain_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChainId).HasColumnName("chain_id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.ExecutionOrder).HasColumnName("execution_order");
            entity.Property(e => e.DependsOnMemberId).HasColumnName("depends_on_member_id");
            entity.Property(e => e.RunOnUpstreamFailure).HasColumnName("run_on_upstream_failure").HasDefaultValue(false);

            entity.HasOne(e => e.Chain).WithMany(c => c.Members).HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DependsOnMember).WithMany().HasForeignKey(e => e.DependsOnMemberId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.ChainId, e.ExecutionOrder }).IsUnique();
            entity.HasIndex(e => e.JobId);
        });

        modelBuilder.Entity<ChainExecution>(entity =>
        {
            entity.ToTable("chain_executions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChainId).HasColumnName("chain_id");
            entity.Property(e => e.TriggeredBy).HasColumnName("triggered_by").IsRequired();
            entity.Property(e => e.State).HasColumnName("state")
                .HasConversion(
                    v => v.ToString().ToLowerInvariant(),
                    v => Enum.Parse<ChainExecutionState>(v, true));
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Chain).WithMany(c => c.Executions).HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ChainId, e.CreatedAt }).IsDescending(false, true);
            entity.HasIndex(e => e.State);
        });

        modelBuilder.Entity<JobDependency>(entity =>
        {
            entity.ToTable("job_dependencies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UpstreamJobId).HasColumnName("upstream_job_id");
            entity.Property(e => e.DownstreamJobId).HasColumnName("downstream_job_id");
            entity.Property(e => e.RunOnFailure).HasColumnName("run_on_failure").HasDefaultValue(false);

            entity.HasOne(e => e.UpstreamJob).WithMany(j => j.DownstreamDependencies).HasForeignKey(e => e.UpstreamJobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.DownstreamJob).WithMany(j => j.UpstreamDependencies).HasForeignKey(e => e.DownstreamJobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UpstreamJobId, e.DownstreamJobId }).IsUnique();
            entity.HasIndex(e => e.DownstreamJobId);
        });

        modelBuilder.Entity<NotificationRule>(entity =>
        {
            entity.ToTable("notification_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.EventTypes).HasColumnName("event_types").HasColumnType("jsonb");
            entity.Property(e => e.Channel).HasColumnName("channel").IsRequired();
            entity.Property(e => e.ChannelConfig).HasColumnName("channel_config").HasColumnType("jsonb");
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).HasFilter("NOT is_deleted");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasFilter("NOT is_deleted AND is_enabled");
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("notification_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.NotificationRuleId).HasColumnName("notification_rule_id");
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.Channel).HasColumnName("channel").IsRequired();
            entity.Property(e => e.Recipient).HasColumnName("recipient").IsRequired();
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.SentAt).HasColumnName("sent_at");

            entity.HasOne(e => e.NotificationRule).WithMany().HasForeignKey(e => e.NotificationRuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.NotificationRuleId);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.SentAt).IsDescending();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").IsRequired();
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Role).HasColumnName("role").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.IsSsoUser).HasColumnName("is_sso_user").HasDefaultValue(false);
            entity.Property(e => e.SsoProviderId).HasColumnName("sso_provider_id");
            entity.Property(e => e.SsoSubjectId).HasColumnName("sso_subject_id");
            entity.Property(e => e.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue(0);
            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.PasswordChangedAt).HasColumnName("password_changed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.GitHubId).HasColumnName("github_id");
            entity.Property(e => e.GitHubUsername).HasColumnName("github_username");
            entity.Property(e => e.GitHubToken).HasColumnName("github_token");
            entity.Property(e => e.GitHubLinkedAt).HasColumnName("github_linked_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

#pragma warning disable CS0618 // AuthProvider.Users is intentionally kept for EF navigation; use SsoUserLinks for new code
            entity.HasOne(e => e.SsoProvider).WithMany(p => p.Users).HasForeignKey(e => e.SsoProviderId);
#pragma warning restore CS0618
            entity.HasIndex(e => e.Username).IsUnique().HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedByIp).HasColumnName("created_by_ip");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");

            entity.HasOne(e => e.User).WithMany(u => u.RefreshTokens).HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.ReplacedBy).WithMany().HasForeignKey(e => e.ReplacedById);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TokenHash);
        });

        modelBuilder.Entity<AuthProvider>(entity =>
        {
            entity.ToTable("auth_providers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Slug).HasColumnName("slug").IsRequired();
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
            entity.Property(e => e.Configuration).HasColumnName("configuration").HasColumnType("jsonb");
            entity.Property(e => e.AutoProvision).HasColumnName("auto_provision").HasDefaultValue(true);
            entity.Property(e => e.DefaultRole).HasColumnName("default_role").IsRequired();
            entity.Property(e => e.AllowLocalPassword).HasColumnName("allow_local_password").HasDefaultValue(false);
            entity.Property(e => e.RoleMapping).HasColumnName("role_mapping").HasColumnType("jsonb");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name).IsUnique().HasFilter("NOT is_deleted");
            entity.HasIndex(e => e.Slug).IsUnique().HasFilter("NOT is_deleted");
        });

        modelBuilder.Entity<SsoUserLink>(entity =>
        {
            entity.ToTable("sso_user_links");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.LinkedAt).HasColumnName("linked_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

            entity.HasOne(e => e.User)
                .WithMany(u => u.SsoUserLinks)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Provider)
                .WithMany(p => p.SsoUserLinks)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();
        });

        modelBuilder.Entity<JobVersion>(entity =>
        {
            entity.ToTable("job_versions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.VersionNumber).HasColumnName("version_number");
            entity.Property(e => e.ConfigSnapshot).HasColumnName("config_snapshot").HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.Job).WithMany(j => j.Versions).HasForeignKey(e => e.JobId);
            entity.HasIndex(e => new { e.JobId, e.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<DomainEvent>(entity =>
        {
            entity.ToTable("domain_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ProcessedBy).HasColumnName("processed_by");
        });

        modelBuilder.Entity<KeyShareLink>(entity =>
        {
            entity.ToTable("key_share_links");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.KeyId).HasColumnName("key_id");
            entity.Property(e => e.KeyType).HasColumnName("key_type").IsRequired();
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(e => e.TokenSalt).HasColumnName("token_salt").IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");

            entity.HasIndex(e => new { e.KeyId, e.KeyType });
            entity.HasIndex(e => e.TokenHash);
        });

        modelBuilder.Entity<StepCallback>(entity =>
        {
            entity.ToTable("step_callbacks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CallbackKey).HasColumnName("callback_key").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasDefaultValue("pending");
            entity.Property(e => e.ResultPayload).HasColumnName("result_payload").HasColumnType("jsonb");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.HasIndex(e => e.CallbackKey).IsUnique();
        });
    }
}
