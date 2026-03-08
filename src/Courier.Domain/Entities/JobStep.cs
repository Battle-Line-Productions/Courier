namespace Courier.Domain.Entities;

public class JobStep
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public string Configuration { get; set; } = "{}";
    public int TimeoutSeconds { get; set; } = 300;
    public string? Alias { get; set; }

    public Job Job { get; set; } = null!;
}
