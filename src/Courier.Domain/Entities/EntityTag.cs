namespace Courier.Domain.Entities;

public class EntityTag
{
    public Guid Id { get; set; }
    public Guid TagId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Tag Tag { get; set; } = null!;
}
