namespace BDMAgent.Domain.Entities;
public sealed class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Company { get; set; } = "";
    public string? Website { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string Country { get; set; } = "";
    public string Source { get; set; } = "Manual";
    public string? SourceUrl { get; set; }
    public string OpportunityText { get; set; } = "";
    public string Technology { get; set; } = "Unknown";
    public string? ResearchSummary { get; set; }
    public string? PainPoints { get; set; }
    public string? QualificationReason { get; set; }
    public int Score { get; set; }
    public string Status { get; set; } = "New";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string IdentityKey { get; set; } = "";
    public Guid? CampaignId { get; set; }
    public bool Suppressed { get; set; }
    public string? SuppressionReason { get; set; }
    public string? Classification { get; set; }
    public string? ResearchEvidence { get; set; }
    public byte[] Version { get; set; } = [];
}
