using BDMAgent.Domain.Entities;
namespace BDMAgent.Application.Abstractions;
public interface IAiService
{
    Task<ResearchResult> ResearchAsync(Lead lead, CancellationToken ct = default);
    Task<OutreachContent> DraftOutreachAsync(Lead lead, CancellationToken ct = default);
    Task<string> ClassifyReplyAsync(string subject, string body, CancellationToken ct = default);
}
public sealed record ResearchResult(string Summary, string PainPoints, string Technology);
public sealed record OutreachContent(string Subject, string Body);
