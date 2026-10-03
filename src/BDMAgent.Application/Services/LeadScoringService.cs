using BDMAgent.Domain.Entities;
namespace BDMAgent.Application.Services;
public sealed class LeadScoringService
{
    public int Score(Lead lead)
    {
        var text = $"{lead.OpportunityText} {lead.Technology} {lead.ResearchSummary} {lead.PainPoints}".ToLowerInvariant();
        var score = 10;
        if (text.Contains(".net") || text.Contains("c#") || text.Contains("asp.net")) score += 25;
        if (text.Contains("angular")) score += 15;
        if (text.Contains("php")) score += 15;
        if (text.Contains("wordpress")) score += 15;
        if (text.Contains("azure") || text.Contains("aws")) score += 10;
        if (text.Contains("microservice") || text.Contains("api") || text.Contains("integration")) score += 10;
        if (text.Contains("moderniz") || text.Contains("dedicated") || text.Contains("developer")) score += 15;
        if (!string.IsNullOrWhiteSpace(lead.ContactEmail)) score += 10;
        if (!string.IsNullOrWhiteSpace(lead.Website)) score += 5;
        return Math.Min(score, 100);
    }
}
