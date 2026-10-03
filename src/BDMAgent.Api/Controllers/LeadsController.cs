using BDMAgent.Application.Abstractions; using BDMAgent.Application.Services; using BDMAgent.Domain.Entities; using BDMAgent.Infrastructure.Data; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Api.Controllers;
[ApiController][Route("api/leads")] public sealed class LeadsController(BdmDbContext db, LeadScoringService scoring, IAiService ai):ControllerBase
{
 [HttpGet] public async Task<IReadOnlyList<Lead>> Get()=>await db.Leads.OrderByDescending(x=>x.Score).ToListAsync();
 [HttpGet("{id:guid}")] public async Task<ActionResult<Lead>> Get(Guid id)=>await db.Leads.FindAsync(id) is { } x?Ok(x):NotFound();
 [HttpPost] public async Task<ActionResult<Lead>> Create(Lead lead){lead.Score=scoring.Score(lead);db.Leads.Add(lead);await db.SaveChangesAsync();return Ok(lead);}
 [HttpPost("{id:guid}/research")] public async Task<IActionResult> Research(Guid id,CancellationToken ct){var x=await db.Leads.FindAsync(id);if(x is null)return NotFound();var r=await ai.ResearchAsync(x,ct);x.ResearchSummary=r.Summary;x.PainPoints=r.PainPoints;x.Technology=r.Technology;x.Score=scoring.Score(x);x.QualificationReason=x.Score>=55?"Qualified":"Below qualification threshold";x.Status=x.Score>=55?"Qualified":"Nurture";x.UpdatedUtc=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(x);}
 [HttpPost("{id:guid}/draft")] public async Task<IActionResult> Draft(Guid id,CancellationToken ct){var x=await db.Leads.FindAsync(id);if(x is null)return NotFound();var d=await ai.DraftOutreachAsync(x,ct);var draft=new OutreachDraft{LeadId=x.Id,Subject=d.Subject,Body=d.Body};db.OutreachDrafts.Add(draft);x.Status="Drafted";await db.SaveChangesAsync(ct);return Ok(draft);}
}
