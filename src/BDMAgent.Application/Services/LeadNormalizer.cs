using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using BDMAgent.Domain.Entities;
namespace BDMAgent.Application.Services;
public static class LeadNormalizer
{
 public static void Normalize(Lead lead)
 {
  lead.Company=lead.Company.Trim(); lead.OpportunityText=lead.OpportunityText.Trim();
  if(lead.Company.Length is 0 or >200 || lead.OpportunityText.Length is 0 or >20000) throw new ArgumentException("Company and opportunity are required (max 200/20000 characters).");
  if(!string.IsNullOrWhiteSpace(lead.ContactEmail)) { lead.ContactEmail=new MailAddress(lead.ContactEmail.Trim()).Address.ToLowerInvariant(); }
  else lead.ContactEmail=null;
  if(!string.IsNullOrWhiteSpace(lead.Website)) { var uri=new Uri(lead.Website); if(uri.Scheme is not ("https" or "http")) throw new ArgumentException("Website must be HTTP(S)."); lead.Website=uri.GetLeftPart(UriPartial.Path).TrimEnd('/'); }
  var identity=!string.IsNullOrWhiteSpace(lead.SourceUrl) ? lead.SourceUrl.Trim().TrimEnd('/').ToLowerInvariant() : $"{lead.Company.ToLowerInvariant()}|{lead.OpportunityText.ToLowerInvariant()}";
  lead.IdentityKey=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
 }
}
