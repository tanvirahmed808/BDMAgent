using BDMAgent.Domain.Entities; using BDMAgent.Infrastructure.Data; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Api.Controllers;
[ApiController][Route("api/campaigns")] public sealed class CampaignsController(BdmDbContext db):ControllerBase
{
 [HttpGet] public async Task<IReadOnlyList<Campaign>> Get()=>await db.Campaigns.ToListAsync();
 [HttpPost] public async Task<IActionResult> Create(Campaign c){db.Campaigns.Add(c);await db.SaveChangesAsync();return Ok(c);}
}
