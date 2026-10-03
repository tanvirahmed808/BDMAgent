using System.Text.Json;
using BDMAgent.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Infrastructure.Services;
public sealed class GmailSync(BdmDbContext db,GoogleIntegration google,CrmWorkflow workflow)
{
 public async Task<int> Run(CancellationToken ct)
 {
  var account=await db.IntegrationAccounts.FindAsync(["Google"],ct)??throw new InvalidOperationException("Connect Google first.");
  var since=account.LastSyncUtc?.AddDays(-1)??DateTime.UtcNow.AddDays(-30);var started=DateTime.UtcNow;
  var query=Uri.EscapeDataString("in:anywhere -in:sent -in:drafts after:"+new DateTimeOffset(since).ToUnixTimeSeconds());
  string? page=null;var count=0;
  do
  {
   using var list=await google.Request(HttpMethod.Get,$"https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=100&q={query}"+(page is null?"":"&pageToken="+Uri.EscapeDataString(page)),null,ct);
   if(list.RootElement.TryGetProperty("messages",out var messages))foreach(var item in messages.EnumerateArray())
   {
    var id=item.GetProperty("id").GetString()!;
    if(await db.Replies.AnyAsync(x=>x.ExternalMessageId==id,ct))continue;
    using var message=await google.Request(HttpMethod.Get,$"https://gmail.googleapis.com/gmail/v1/users/me/messages/{id}?format=full",null,ct);
    var root=message.RootElement;var payload=root.GetProperty("payload");var headers=payload.GetProperty("headers").EnumerateArray().ToArray();
    string Header(string name)=>headers.FirstOrDefault(x=>x.GetProperty("name").GetString()!.Equals(name,StringComparison.OrdinalIgnoreCase)) is var h&&h.ValueKind!=JsonValueKind.Undefined?h.GetProperty("value").GetString()??"":"";
    var from=Header("From");if(!System.Net.Mail.MailAddress.TryCreate(from,out var address))continue;
    var email=address.Address.ToLowerInvariant();var thread=root.GetProperty("threadId").GetString();
    if(!await db.Leads.AnyAsync(x=>x.ContactEmail==email,ct)&&!await db.OutreachDrafts.AnyAsync(x=>x.ThreadId==thread,ct))continue;
    await workflow.Ingest(email,Header("Subject"),Body(payload),id,thread,ct);count++;
   }
   page=list.RootElement.TryGetProperty("nextPageToken",out var next)?next.GetString():null;
  }while(page is not null);
  account.LastSyncUtc=started;await db.SaveChangesAsync(ct);return count;
 }
 private static string Body(JsonElement part)
 {
  if(part.TryGetProperty("mimeType",out var mime)&&mime.GetString()=="text/plain"&&part.TryGetProperty("body",out var body)&&body.TryGetProperty("data",out var data))return GoogleIntegration.Decode(data.GetString()!);
  if(part.TryGetProperty("parts",out var children)) {var text=string.Join("\n",children.EnumerateArray().Select(Body));if(!string.IsNullOrWhiteSpace(text))return text;}
  if(part.TryGetProperty("body",out var fallback)&&fallback.TryGetProperty("data",out var raw))return GoogleIntegration.Decode(raw.GetString()!);
  return "";
 }
 public async Task<bool> Reconcile(Guid id,CancellationToken ct)
 {
  var draft=await db.OutreachDrafts.FindAsync([id],ct)??throw new KeyNotFoundException();
  if(draft.Status is not ("DeliveryUnknown" or "Sending"))throw new ArgumentException("Only uncertain deliveries require reconciliation.");
  using var list=await google.Request(HttpMethod.Get,"https://gmail.googleapis.com/gmail/v1/users/me/messages?q="+Uri.EscapeDataString("in:sent rfc822msgid:"+draft.RfcMessageId),null,ct);
  if(!list.RootElement.TryGetProperty("messages",out var messages)||messages.GetArrayLength()==0)return false;
  draft.ExternalMessageId=messages[0].GetProperty("id").GetString();draft.ThreadId=messages[0].GetProperty("threadId").GetString();draft.Status="Sent";draft.SentUtc=DateTime.UtcNow;
  var lead=await workflow.Lead(draft.LeadId,ct);if(!lead.Suppressed)lead.Status="Contacted";await db.SaveChangesAsync(ct);return true;
 }
}
