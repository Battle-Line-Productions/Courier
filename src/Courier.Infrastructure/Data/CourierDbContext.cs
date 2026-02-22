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
    }
}
