namespace BDMAgent.Domain.Entities;
public sealed class FollowUp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public int SequenceNo { get; set; }
    public DateTime DueUtc { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
