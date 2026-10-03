namespace BDMAgent.Domain.Entities;
public sealed class OutreachDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "Draft"; // Draft, Approved, Rejected, Sent, Failed
    public string Channel { get; set; } = "Email";
    public string? ExternalMessageId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public string? ThreadId { get; set; }
    public string? RfcMessageId { get; set; }
    public Guid? FollowUpId { get; set; }
    public string Kind { get; set; } = "Initial";
    public string? ApprovedBy { get; set; }
    public string? LastError { get; set; }
    public byte[] Version { get; set; } = [];
}
