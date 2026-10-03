using BDMAgent.Domain.Entities; using BDMAgent.Infrastructure.Data; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Api.Controllers;
[ApiController][Route("api/meetings")] public sealed class MeetingsController(BdmDbContext db):ControllerBase
{
 [HttpGet] public async Task<IReadOnlyList<Meeting>> Get()=>await db.Meetings.OrderBy(x=>x.StartsUtc).ToListAsync();
 [HttpPost] public async Task<IActionResult> Create(Meeting m){db.Meetings.Add(m);var lead=await db.Leads.FindAsync(m.LeadId);if(lead is not null)lead.Status="Meeting";await db.SaveChangesAsync();return Ok(m);}
}
