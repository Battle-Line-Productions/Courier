namespace Courier.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public string FailurePolicy { get; set; } = """{"type":"stop","max_retries":3,"backoff_base_seconds":1,"backoff_max_seconds":60}""";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<JobStep> Steps { get; set; } = [];
    public List<JobExecution> Executions { get; set; } = [];
    public List<JobSchedule> Schedules { get; set; } = [];
}
