namespace BDMAgent.Domain.Entities;
public sealed class Reply
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? LeadId { get; set; }
    public string FromEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Classification { get; set; } = "Unknown";
    public string? ExternalMessageId { get; set; }
    public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;
    public string? ThreadId { get; set; }
}
