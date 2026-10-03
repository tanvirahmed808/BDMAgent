using BDMAgent.Infrastructure.Data; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Api.Controllers;
[ApiController][Route("api/dashboard")] public sealed class DashboardController(BdmDbContext db):ControllerBase
{
 [HttpGet] public async Task<IActionResult> Get(){var leads=await db.Leads.CountAsync();var qualified=await db.Leads.CountAsync(x=>x.Score>=55);var sent=await db.OutreachDrafts.CountAsync(x=>x.Status=="Sent");var interested=await db.Leads.CountAsync(x=>x.Status=="Interested");var pending=await db.OutreachDrafts.CountAsync(x=>x.Status=="Draft");return Ok(new{leads,qualified,sent,interested,pendingApproval=pending});}
}
