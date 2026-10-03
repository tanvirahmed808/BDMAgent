namespace BDMAgent.Domain.Entities;
public sealed class AuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
