namespace BDMAgent.Domain.Entities;
public sealed class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public string Title { get; set; } = "Discovery Call";
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public string? ExternalEventId { get; set; }
    public string Status { get; set; } = "Proposed";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
