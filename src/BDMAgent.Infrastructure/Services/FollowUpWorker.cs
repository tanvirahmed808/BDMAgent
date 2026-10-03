using BDMAgent.Domain.Entities;
using BDMAgent.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace BDMAgent.Infrastructure.Services;
public sealed class FollowUpWorker(IServiceScopeFactory scopes,IConfiguration cfg,ILogger<FollowUpWorker> log):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct)
 {
  if(!cfg.GetValue("Automation:Enabled",false))return;
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(30,cfg.GetValue("Automation:IntervalSeconds",300))));
  do
  {
   await Run("Discovery",async s=>{await s.GetRequiredService<DiscoveryService>().Run(ct);},ct);
   await Run("GmailSync",async s=>{if(await s.GetRequiredService<BdmDbContext>().IntegrationAccounts.AnyAsync(ct))await s.GetRequiredService<GmailSync>().Run(ct);},ct);
   await Run("ResearchDraft",async s=>
   {
    if(string.IsNullOrWhiteSpace(cfg["AI:ApiKey"]))return;
    var db=s.GetRequiredService<BdmDbContext>();var workflow=s.GetRequiredService<CrmWorkflow>();
    foreach(var id in await db.Leads.Where(x=>x.Status=="New"&&!x.Suppressed).OrderBy(x=>x.CreatedUtc).Take(10).Select(x=>x.Id).ToListAsync(ct))await workflow.Research(id,ct);
    foreach(var id in await db.Leads.Where(x=>x.Status=="Qualified"&&!x.Suppressed).Take(10).Select(x=>x.Id).ToListAsync(ct))await workflow.Draft(id,"Initial",ct);
   },ct);
   await Run("FollowUps",s=>s.GetRequiredService<CrmWorkflow>().ProcessFollowUps(ct),ct);
  }while(await timer.WaitForNextTickAsync(ct));
 }
 private async Task Run(string type,Func<IServiceProvider,Task> action,CancellationToken ct)
 {
  using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<BdmDbContext>();
  var job=new AgentJob{AgentType=type,Status="Running"};
  try{db.AgentJobs.Add(job);await db.SaveChangesAsync(ct);await action(scope.ServiceProvider);job.Status="Completed";}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
  catch(Exception ex){log.LogError(ex,"Agent {Type} failed",type);db.ChangeTracker.Clear();db.AgentJobs.Attach(job);job.Status="Failed";job.PayloadJson="{\"error\":\"See server logs for failure details\"}";}
  job.CompletedUtc=DateTime.UtcNow;try{await db.SaveChangesAsync(ct);}catch(Exception ex){log.LogError(ex,"Could not persist agent run");}
 }
}
