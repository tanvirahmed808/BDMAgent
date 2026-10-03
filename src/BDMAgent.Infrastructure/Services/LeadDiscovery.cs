using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using BDMAgent.Domain.Entities;
using Microsoft.Extensions.Configuration;
namespace BDMAgent.Infrastructure.Services;
public interface ILeadSource { string Kind {get;} Task<IReadOnlyList<Lead>> Discover(SourceOptions options,CancellationToken ct); }
public sealed class SourceOptions
{
 public string Name {get;set;}="";
 public string Kind {get;set;}="";
 public string Url {get;set;}="";
 public string? ApiKey {get;set;}
 public bool Enabled {get;set;}
 public bool TermsAccepted {get;set;}
 public Guid? CampaignId {get;set;}
}
public static class SourceFilter
{
 public static bool Relevant(string text)=>new[]{".net","c#","angular","php","wordpress","software","api ","cloud","modernization","modernisation","developer","application development"}.Any(x=>text.Contains(x,StringComparison.OrdinalIgnoreCase));
 public static void Validate(SourceOptions options)
 {
  if(!options.TermsAccepted)throw new ArgumentException("Provider terms must be accepted by operator.");
  if(!Uri.TryCreate(options.Url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.IsLoopback||System.Net.IPAddress.TryParse(uri.Host,out _))throw new ArgumentException("Source must use an operator-configured public HTTPS hostname.");
 }
}
public sealed class RssLeadSource(HttpClient http):ILeadSource
{
 public string Kind=>"Rss";
 public async Task<IReadOnlyList<Lead>> Discover(SourceOptions options,CancellationToken ct)
 {
  SourceFilter.Validate(options);
  using var stream=await http.GetStreamAsync(options.Url,ct);
  using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000,Async=true});
  var doc=await XDocument.LoadAsync(reader,LoadOptions.None,ct);
  return doc.Descendants().Where(x=>x.Name.LocalName is "item" or "entry").Take(100).Select(x=>
  {
   string Field(string key)=>x.Elements().FirstOrDefault(e=>e.Name.LocalName==key)?.Value??"";
   var link=x.Elements().FirstOrDefault(e=>e.Name.LocalName=="link");
   return new Lead{Company=Field("title").Length>200?Field("title")[..200]:Field("title"),OpportunityText=Field("title")+"\n"+(Field("description")+Field("summary")+Field("content")),Source=options.Name,SourceUrl=link?.Attribute("href")?.Value??link?.Value,CampaignId=options.CampaignId};
  }).Where(x=>SourceFilter.Relevant(x.OpportunityText)).ToList();
 }
}
public sealed class JsonLeadSource(HttpClient http):ILeadSource
{
 public string Kind=>"Json";
 public async Task<IReadOnlyList<Lead>> Discover(SourceOptions options,CancellationToken ct)
 {
  SourceFilter.Validate(options);
  using var req=new HttpRequestMessage(HttpMethod.Get,options.Url);
  if(!string.IsNullOrWhiteSpace(options.ApiKey))req.Headers.Authorization=new("Bearer",options.ApiKey);
  using var res=await http.SendAsync(req,ct);res.EnsureSuccessStatusCode();
  var rows=await res.Content.ReadFromJsonAsync<List<Lead>>(ct)??[];
  return rows.Take(100).Where(x=>SourceFilter.Relevant(x.OpportunityText)).Select(x=>{x.Source=options.Name;x.CampaignId=options.CampaignId;return x;}).ToList();
 }
}
public sealed class SamGovLeadSource(HttpClient http):ILeadSource
{
 public string Kind=>"SamGov";
 public async Task<IReadOnlyList<Lead>> Discover(SourceOptions options,CancellationToken ct)
 {
  if(!options.TermsAccepted||string.IsNullOrWhiteSpace(options.ApiKey))throw new ArgumentException("SAM.gov API key and terms acceptance required.");
  var url=$"https://api.sam.gov/opportunities/v2/search?api_key={Uri.EscapeDataString(options.ApiKey)}&postedFrom={DateTime.UtcNow.AddDays(-7):MM/dd/yyyy}&postedTo={DateTime.UtcNow:MM/dd/yyyy}&limit=100&ncode=541511";
  using var res=await http.GetAsync(url,ct);res.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
  if(!doc.RootElement.TryGetProperty("opportunitiesData",out var data))return [];
  string Get(JsonElement x,string key)=>x.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
  return data.EnumerateArray().Select(x=>new Lead{Company=Get(x,"fullParentPathName") is {} name&&name.Length>0?name[..Math.Min(200,name.Length)]:"US Federal Agency",OpportunityText=Get(x,"title")+"\nNotice ID: "+Get(x,"noticeId"),Country="USA",Source=options.Name,SourceUrl=Get(x,"uiLink"),CampaignId=options.CampaignId}).Where(x=>SourceFilter.Relevant(x.OpportunityText)).ToList();
 }
}
public sealed class DiscoveryService(IEnumerable<ILeadSource> adapters,IConfiguration cfg,CrmWorkflow workflow)
{
 public async Task<int> Run(CancellationToken ct)
 {
  var total=0;
  foreach(var options in cfg.GetSection("Sources").Get<List<SourceOptions>>()??[])
  {
   if(!options.Enabled)continue;
   var adapter=adapters.FirstOrDefault(x=>x.Kind==options.Kind)??throw new ArgumentException("Unknown lead adapter: "+options.Kind);
   foreach(var lead in await adapter.Discover(options,ct)){await workflow.Import(lead,ct);total++;}
  }
  return total;
 }
}
