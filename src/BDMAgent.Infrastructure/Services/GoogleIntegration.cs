using System.Net.Http.Headers;
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
