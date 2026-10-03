from pathlib import Path
def put(p,s): Path(p).write_text(s)
put('src/BDMAgent.Infrastructure/Services/GoogleIntegration.cs',r'''using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Mail;
using BDMAgent.Domain.Entities;
using BDMAgent.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Infrastructure.Services;
public sealed class GoogleIntegration(HttpClient http, BdmDbContext db, IDataProtectionProvider protection, IConfiguration cfg)
{
 private readonly IDataProtector protector=protection.CreateProtector("BDM.Google.Refresh.v1");
 private string Required(string key)=>!string.IsNullOrWhiteSpace(cfg[key]) ? cfg[key]! : throw new InvalidOperationException($"Configure {key} first.");
 public string AuthorizationUrl()
 {
  var state=protection.CreateProtector("BDM.Google.State.v1").Protect(DateTime.UtcNow.AddMinutes(10).Ticks+"|"+Guid.NewGuid());
  var scope="https://www.googleapis.com/auth/gmail.send https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.freebusy";
  return "https://accounts.google.com/o/oauth2/v2/auth?"+Query(new(){["client_id"]=Required("Google:ClientId"),["redirect_uri"]=Required("Google:RedirectUri"),["response_type"]="code",["scope"]=scope,["access_type"]="offline",["prompt"]="consent",["state"]=state});
 }
 public async Task Connect(string code,string state,CancellationToken ct)
 {
  var decoded=protection.CreateProtector("BDM.Google.State.v1").Unprotect(state);
  if(!long.TryParse(decoded.Split('|')[0],out var ticks)||new DateTime(ticks,DateTimeKind.Utc)<DateTime.UtcNow) throw new ArgumentException("OAuth state expired.");
  using var token=await Token(new(){["code"]=code,["grant_type"]="authorization_code",["redirect_uri"]=Required("Google:RedirectUri")},ct);
  if(!token.RootElement.TryGetProperty("refresh_token",out var refresh)) throw new InvalidOperationException("No refresh token; revoke previous grant and reconnect with offline consent.");
  using var req=new HttpRequestMessage(HttpMethod.Get,"https://gmail.googleapis.com/gmail/v1/users/me/profile");
  req.Headers.Authorization=new("Bearer",token.RootElement.GetProperty("access_token").GetString());
  using var res=await http.SendAsync(req,ct);res.EnsureSuccessStatusCode();
  using var profile=JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
  var account=await db.IntegrationAccounts.FindAsync(["Google"],ct);
  if(account is null){account=new();db.IntegrationAccounts.Add(account);}
  account.ProtectedRefreshToken=protector.Protect(refresh.GetString()!);account.Email=profile.RootElement.GetProperty("emailAddress").GetString()!;
  await db.SaveChangesAsync(ct);
 }
 private async Task<JsonDocument> Token(Dictionary<string,string> values,CancellationToken ct)
 {
  values["client_id"]=Required("Google:ClientId");values["client_secret"]=Required("Google:ClientSecret");
  using var res=await http.PostAsync("https://oauth2.googleapis.com/token",new FormUrlEncodedContent(values),ct);
  res.EnsureSuccessStatusCode();return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
 }
 private async Task<string> Access(CancellationToken ct)
 {
  var account=await db.IntegrationAccounts.FindAsync(["Google"],ct)??throw new InvalidOperationException("Connect Google OAuth first.");
  using var token=await Token(new(){["grant_type"]="refresh_token",["refresh_token"]=protector.Unprotect(account.ProtectedRefreshToken)},ct);
  return token.RootElement.GetProperty("access_token").GetString()!;
 }
 public async Task<JsonDocument> Request(HttpMethod method,string url,object? body,CancellationToken ct)
 {
  var access=await Access(ct);
  // Retry reads only. Outbound messages are never blindly retried after uncertain delivery.
  for(var attempt=0;;attempt++)
  {
   using var req=new HttpRequestMessage(method,url);req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access);
   if(body is not null)req.Content=JsonContent.Create(body);
   using var res=await http.SendAsync(req,ct);
   if(method==HttpMethod.Get && attempt<3 && ((int)res.StatusCode==429||(int)res.StatusCode>=500)) {await Task.Delay(TimeSpan.FromSeconds(Math.Min(res.Headers.RetryAfter?.Delta?.TotalSeconds??Math.Pow(2,attempt),30)),ct);continue;}
   res.EnsureSuccessStatusCode();return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
  }
 }
 public async Task<(string Id,string Thread)> Send(Lead lead,OutreachDraft draft,CancellationToken ct)
 {
  var account=await db.IntegrationAccounts.FindAsync(["Google"],ct)??throw new InvalidOperationException("Connect Google first.");
  static string Header(string s)=>s.Contains('\r')||s.Contains('\n')?throw new ArgumentException("Invalid mail header."):s;
  var from=new MailAddress(account.Email).Address;var to=new MailAddress(lead.ContactEmail!).Address;
  var references="";
  if(draft.Kind=="FollowUp")
  {
   var previous=await db.OutreachDrafts.Where(x=>x.LeadId==lead.Id&&x.Status=="Sent").OrderByDescending(x=>x.SentUtc).FirstOrDefaultAsync(ct);
   draft.ThreadId=previous?.ThreadId;
   if(previous?.RfcMessageId is not null)references=$"In-Reply-To: {Header(previous.RfcMessageId)}\r\nReferences: {Header(previous.RfcMessageId)}\r\n";
  }
  draft.RfcMessageId??=$"<{draft.Id:N}@bdm-agent.local>";
  var mime=$"From: {Header(from)}\r\nTo: {Header(to)}\r\nSubject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(Header(draft.Subject)))}?=\r\nMessage-ID: {draft.RfcMessageId}\r\n{references}MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n{Convert.ToBase64String(Encoding.UTF8.GetBytes(draft.Body))}";
  var payload=new Dictionary<string,object>{["raw"]=Base64(Encoding.UTF8.GetBytes(mime))};if(draft.ThreadId is not null)payload["threadId"]=draft.ThreadId;
  using var result=await Request(HttpMethod.Post,"https://gmail.googleapis.com/gmail/v1/users/me/messages/send",payload,ct);
  return(result.RootElement.GetProperty("id").GetString()!,result.RootElement.GetProperty("threadId").GetString()!);
 }
 public async Task<string> Book(Lead lead,Meeting meeting,CancellationToken ct)
 {
  var calendar=cfg["Google:CalendarId"]??"primary";
  using var busy=await Request(HttpMethod.Post,"https://www.googleapis.com/calendar/v3/freeBusy",new{timeMin=meeting.StartsUtc.ToString("O"),timeMax=meeting.EndsUtc.ToString("O"),items=new[]{new{id=calendar}}},ct);
  var entry=busy.RootElement.GetProperty("calendars").GetProperty(calendar);
  if(entry.TryGetProperty("errors",out _) || entry.GetProperty("busy").GetArrayLength()>0)throw new ArgumentException("Calendar unavailable or time is busy.");
  var eventId=meeting.Id.ToString("N");
  using var res=await Request(HttpMethod.Post,$"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendar)}/events?sendUpdates=all",new{id=eventId,summary=meeting.Title,start=new{dateTime=meeting.StartsUtc.ToString("O")},end=new{dateTime=meeting.EndsUtc.ToString("O")},attendees=new[]{new{email=lead.ContactEmail}}},ct);
  return res.RootElement.GetProperty("id").GetString()!;
 }
 public static string Base64(byte[] data)=>Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');
 public static string Decode(string value)=>Encoding.UTF8.GetString(Convert.FromBase64String(value.Replace('-','+').Replace('_','/').PadRight((value.Length+3)/4*4,'=')));
 private static string Query(Dictionary<string,string> pairs)=>string.Join("&",pairs.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));
}
''')
put('src/BDMAgent.Infrastructure/Services/CrmWorkflow.cs',r'''using BDMAgent.Application.Abstractions;
using BDMAgent.Application.Services;
using BDMAgent.Domain.Entities;
using BDMAgent.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace BDMAgent.Infrastructure.Services;
public sealed class CrmWorkflow(BdmDbContext db,IAiService ai,LeadScoringService scoring,GoogleIntegration google,IConfiguration cfg)
{
 public static readonly string[] Stages=["New","Researched","Qualified","Outreach Ready","Contacted","Replied","Meeting","Proposal","Won","Lost"];
 public async Task<Lead> Import(Lead lead,CancellationToken ct)
 {
  LeadNormalizer.Normalize(lead);
  var existing=await db.Leads.FirstOrDefaultAsync(x=>x.IdentityKey==lead.IdentityKey,ct);
  if(existing is not null)return existing;
  lead.Id=Guid.NewGuid();lead.Status="New";lead.Suppressed=false;lead.Score=scoring.Score(lead);
  if(lead.ContactEmail is not null && await db.Suppressions.AnyAsync(x=>x.Email==lead.ContactEmail,ct)){lead.Suppressed=true;lead.SuppressionReason="Address suppression list";}
  db.Leads.Add(lead);await db.SaveChangesAsync(ct);return lead;
 }
 public async Task<Lead> Lead(Guid id,CancellationToken ct)=>await db.Leads.FindAsync([id],ct)??throw new KeyNotFoundException("Lead not found.");
 public async Task<Campaign?> Campaign(Lead lead,CancellationToken ct)=>lead.CampaignId is {} id?await db.Campaigns.FindAsync([id],ct):await db.Campaigns.OrderBy(x=>x.CreatedUtc).FirstOrDefaultAsync(x=>x.IsActive,ct);
 public async Task<Lead> Research(Guid id,CancellationToken ct)
 {
  var lead=await Lead(id,ct);if(lead.Suppressed)throw new ArgumentException("Lead is suppressed.");
  var result=await ai.ResearchAsync(lead,ct);lead.ResearchSummary=result.Summary;lead.PainPoints=result.PainPoints;lead.Technology=result.Technology;
  lead.ResearchEvidence=lead.SourceUrl??"Provided opportunity text";lead.Classification=result.Technology;
  lead.Score=scoring.Score(lead);var campaign=await Campaign(lead,ct);lead.CampaignId??=campaign?.Id;
  if(lead.Status is "New" or "Researched" or "Qualified")lead.Status=lead.Score>=(campaign?.MinimumScore??55)?"Qualified":"Researched";
  lead.QualificationReason=$"Score {lead.Score}; threshold {campaign?.MinimumScore??55}; technology {lead.Technology}";lead.UpdatedUtc=DateTime.UtcNow;
  await db.SaveChangesAsync(ct);return lead;
 }
 public async Task<OutreachDraft> Draft(Guid id,string kind,CancellationToken ct)
 {
  var lead=await Lead(id,ct);if(lead.Suppressed)throw new ArgumentException("Lead is suppressed.");
  if(kind=="Initial"&&lead.Status!="Qualified")throw new ArgumentException("Research and qualify before outreach.");
  if(kind=="Proposal"&&lead.Status is not ("Replied" or "Meeting" or "Proposal"))throw new ArgumentException("Proposal requires a conversation or meeting.");
  var pending=await db.OutreachDrafts.FirstOrDefaultAsync(x=>x.LeadId==id&&x.Kind==kind&&(x.Status=="Draft"||x.Status=="Approved"),ct);
  if(pending is not null)return pending;
  var campaign=await Campaign(lead,ct);var context=lead.OpportunityText;
  // Capability statements are supplied by the operator, never invented by the model.
  lead.OpportunityText=context+"\nOur verified capabilities: "+(campaign?.Capabilities??cfg["Business:Capabilities"])+"\nContent type: "+kind;
  OutreachContent content;try{content=await ai.DraftOutreachAsync(lead,ct);}finally{lead.OpportunityText=context;}
  var draft=new OutreachDraft{LeadId=id,Subject=content.Subject,Body=content.Body,Kind=kind};db.OutreachDrafts.Add(draft);
  lead.Status=kind=="Proposal"?"Proposal":"Outreach Ready";await db.SaveChangesAsync(ct);return draft;
 }
 public async Task<OutreachDraft> Decision(Guid id,bool approve,string actor,CancellationToken ct)
 {
  var draft=await db.OutreachDrafts.FindAsync([id],ct)??throw new KeyNotFoundException();
  if(draft.Status!="Draft")throw new ArgumentException("Only pending drafts can be reviewed.");
  draft.Status=approve?"Approved":"Rejected";draft.ApprovedUtc=approve?DateTime.UtcNow:null;draft.ApprovedBy=actor;await db.SaveChangesAsync(ct);return draft;
 }
 public async Task<OutreachDraft> Send(Guid id,CancellationToken ct)
 {
  var draft=await db.OutreachDrafts.FindAsync([id],ct)??throw new KeyNotFoundException();
  var lead=await Lead(draft.LeadId,ct);var campaign=await Campaign(lead,ct);
  if(draft.Status!="Approved")throw new ArgumentException("Message must be approved before sending.");
  if(campaign is {IsActive:false})throw new ArgumentException("Campaign paused.");
  if(lead.ContactEmail is null||lead.Suppressed||await db.Suppressions.AnyAsync(x=>x.Email==lead.ContactEmail,ct))throw new ArgumentException("Address missing or suppressed.");
  if(draft.Kind=="FollowUp"&&await db.Replies.AnyAsync(x=>x.LeadId==lead.Id,ct))throw new ArgumentException("Follow-ups suppressed after any reply.");
  if(await db.OutreachDrafts.CountAsync(x=>x.SentUtc>=DateTime.UtcNow.Date || x.Status=="Sending",ct)>=(campaign?.DailySendLimit??20))throw new ArgumentException("Daily sending limit reached.");
  if(!await db.IntegrationAccounts.AnyAsync(x=>x.Id=="Google",ct))throw new InvalidOperationException("Connect Gmail before sending.");
  draft.Status="Sending";draft.RfcMessageId=$"<{draft.Id:N}@bdm-agent.local>";await db.SaveChangesAsync(ct); // rowversion prevents competing claims
  try
  {
   // Recheck suppression after the persistent claim, immediately before the remote call.
   await db.Entry(lead).ReloadAsync(ct);
   if(lead.Suppressed||await db.Suppressions.AnyAsync(x=>x.Email==lead.ContactEmail,ct)||draft.Kind=="FollowUp"&&await db.Replies.AnyAsync(x=>x.LeadId==lead.Id,ct))throw new ArgumentException("Sending suppressed.");
   var sent=await google.Send(lead,draft,ct);draft.ExternalMessageId=sent.Id;draft.ThreadId=sent.Thread;draft.Status="Sent";draft.SentUtc=DateTime.UtcNow;lead.Status=draft.Kind=="Proposal"?"Proposal":"Contacted";
   if(draft.Kind=="Initial")
   {
    var days=(campaign?.FollowUpDays??"3,7,14").Split(',').Select(int.Parse).ToArray();
    for(var i=0;i<days.Length;i++)db.FollowUps.Add(new(){LeadId=lead.Id,SequenceNo=i+1,DueUtc=DateTime.UtcNow.AddDays(days[i]),Subject="Re: "+draft.Subject,Body=$"Hi {lead.ContactName??"there"},\n\nFollowing up on {lead.OpportunityText}. Is engineering support still useful? If you prefer no further contact, reply to let us know."});
   }
   await db.SaveChangesAsync(ct);return draft;
  }
  catch(Exception)
  {
   draft.Status="DeliveryUnknown";draft.LastError="Delivery may have occurred. Reconcile Gmail by RFC Message-ID before any retry.";
   await db.SaveChangesAsync(CancellationToken.None);throw;
  }
 }
 public async Task<Reply> Ingest(string from,string subject,string body,string? external,string? thread,CancellationToken ct)
 {
  from=new System.Net.Mail.MailAddress(from).Address.ToLowerInvariant();
  if(external is not null && await db.Replies.FirstOrDefaultAsync(x=>x.ExternalMessageId==external,ct) is {} previous)return previous;
  var lead=thread is not null?await db.Leads.FirstOrDefaultAsync(x=>db.OutreachDrafts.Any(d=>d.LeadId==x.Id&&d.ThreadId==thread),ct):null;
  lead??=await db.Leads.FirstOrDefaultAsync(x=>x.ContactEmail==from,ct);
  var classification=await ai.ClassifyReplyAsync(subject,body,ct);
  var reply=new Reply{LeadId=lead?.Id,FromEmail=from,Subject=subject,Body=body,Classification=classification,ExternalMessageId=external,ThreadId=thread};db.Replies.Add(reply);
  var related=await db.Leads.Where(x=>x.ContactEmail==from).ToListAsync(ct);
  foreach(var item in related)
  {
   var pending=await db.FollowUps.Where(x=>x.LeadId==item.Id&&(x.Status=="Pending"||x.Status=="Drafted")).ToListAsync(ct);pending.ForEach(x=>x.Status="Cancelled");
   var drafts=await db.OutreachDrafts.Where(x=>x.LeadId==item.Id&&(x.Status=="Draft"||x.Status=="Approved")&&(x.Kind=="FollowUp"||classification=="Unsubscribe"||classification=="NotInterested")).ToListAsync(ct);drafts.ForEach(x=>x.Status="Cancelled");
   if(classification is "Unsubscribe" or "NotInterested"){item.Suppressed=true;item.SuppressionReason=classification;item.Status="Lost";}
  }
  if(lead is not null && !lead.Suppressed && lead.Status is not ("Won" or "Lost"))lead.Status="Replied";
  if(classification is "Unsubscribe" or "NotInterested" && !await db.Suppressions.AnyAsync(x=>x.Email==from,ct))db.Suppressions.Add(new(){Email=from,Reason=classification});
  await db.SaveChangesAsync(ct);return reply;
 }
 public async Task ProcessFollowUps(CancellationToken ct)
 {
  var due=await db.FollowUps.Where(x=>x.Status=="Pending"&&x.DueUtc<=DateTime.UtcNow).OrderBy(x=>x.DueUtc).Take(20).ToListAsync(ct);
  foreach(var item in due)
  {
   var lead=await Lead(item.LeadId,ct);var campaign=await Campaign(lead,ct);
   if(lead.Suppressed||lead.Status is "Won" or "Lost"||await db.Replies.AnyAsync(x=>x.LeadId==lead.Id,ct)){item.Status="Cancelled";continue;}
   if(campaign is {IsActive:false})continue;
   var automatic=campaign?.AutoSendFollowUps==true;
   var draft=new OutreachDraft{LeadId=lead.Id,FollowUpId=item.Id,Kind="FollowUp",Subject=item.Subject,Body=item.Body,Status=automatic?"Approved":"Draft",ApprovedBy=automatic?"Campaign policy":null,ApprovedUtc=automatic?DateTime.UtcNow:null};
   db.OutreachDrafts.Add(draft);item.Status="Drafted";await db.SaveChangesAsync(ct);
   if(automatic)await Send(draft.Id,ct);
  }
  await db.SaveChangesAsync(ct);
 }
}
''')
