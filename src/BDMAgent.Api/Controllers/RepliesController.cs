using BDMAgent.Application.Abstractions; using BDMAgent.Domain.Entities; using BDMAgent.Infrastructure.Data; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Api.Controllers;
public sealed record ReplyInput(string FromEmail,string Subject,string Body,string? ExternalMessageId);
[ApiController][Route("api/replies")] public sealed class RepliesController(BdmDbContext db,IAiService ai):ControllerBase
{
 [HttpPost("ingest")] public async Task<IActionResult> Ingest(ReplyInput input,CancellationToken ct){if(input.ExternalMessageId is not null && await db.Replies.AnyAsync(x=>x.ExternalMessageId==input.ExternalMessageId,ct))return Conflict("Already ingested");var lead=await db.Leads.FirstOrDefaultAsync(x=>x.ContactEmail==input.FromEmail,ct);var c=await ai.ClassifyReplyAsync(input.Subject,input.Body,ct);var r=new Reply{LeadId=lead?.Id,FromEmail=input.FromEmail,Subject=input.Subject,Body=input.Body,ExternalMessageId=input.ExternalMessageId,Classification=c};db.Replies.Add(r);if(lead is not null){lead.Status=c is "Interested" or "MeetingRequested"?"Interested":c=="NotInterested"?"ClosedLost":c=="Unsubscribe"?"DoNotContact":lead.Status;if(c is "Interested" or "MeetingRequested")db.FollowUps.Where(x=>x.LeadId==lead.Id&&x.Status=="Pending").ToList().ForEach(x=>x.Status="Cancelled");}await db.SaveChangesAsync(ct);return Ok(r);}
}
