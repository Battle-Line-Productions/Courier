namespace Courier.Domain.Entities;

public class JobChain
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<JobChainMember> Members { get; set; } = [];
    public List<ChainExecution> Executions { get; set; } = [];
    public List<ChainSchedule> Schedules { get; set; } = [];
}
