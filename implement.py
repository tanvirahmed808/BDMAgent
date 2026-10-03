from pathlib import Path
p=Path('src/BDMAgent.Domain/Entities')
def add(file,text):
 f=p/file; s=f.read_text(); s=s.replace('\n}', '\n'+text+'\n}'); f.write_text(s)
add('Lead.cs','''    public string IdentityKey { get; set; } = "";
    public Guid? CampaignId { get; set; }
    public bool Suppressed { get; set; }
    public string? SuppressionReason { get; set; }
    public string? Classification { get; set; }
    public string? ResearchEvidence { get; set; }
    public byte[] Version { get; set; } = [];''')
add('OutreachDraft.cs','''    public string? ThreadId { get; set; }
    public string? RfcMessageId { get; set; }
    public Guid? FollowUpId { get; set; }
    public string Kind { get; set; } = "Initial";
    public string? ApprovedBy { get; set; }
    public string? LastError { get; set; }
    public byte[] Version { get; set; } = [];''')
add('Reply.cs','    public string? ThreadId { get; set; }')
add('Campaign.cs','''    public string Capabilities { get; set; } = ".NET/C#, Angular, PHP, WordPress, API integration, cloud modernization and dedicated developers";
    public string FollowUpDays { get; set; } = "3,7,14";
    public bool AutoSendFollowUps { get; set; }
    public int DailySendLimit { get; set; } = 20;''')
(p/'IntegrationAccount.cs').write_text('''namespace BDMAgent.Domain.Entities;
public sealed class IntegrationAccount
{
 public string Id { get; set; } = "Google";
 public string ProtectedRefreshToken { get; set; } = "";
 public string Email { get; set; } = "";
 public DateTime? LastSyncUtc { get; set; }
}
public sealed class Suppression
{
 public string Email { get; set; } = "";
 public string Reason { get; set; } = "";
 public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
''')
f=Path('src/BDMAgent.Infrastructure/Data/BdmDbContext.cs');s=f.read_text().replace('    protected override','    public DbSet<IntegrationAccount> IntegrationAccounts => Set<IntegrationAccount>();\n    public DbSet<Suppression> Suppressions => Set<Suppression>();\n    protected override');s=s.replace('        b.Entity<Lead>().Property', '''        b.Entity<IntegrationAccount>().HasKey(x => x.Id);
        b.Entity<Suppression>().HasKey(x => x.Email);
        b.Entity<Suppression>().Property(x => x.Email).HasMaxLength(320);
        b.Entity<Lead>().Property(x => x.IdentityKey).HasMaxLength(64);
        b.Entity<Lead>().HasIndex(x => x.IdentityKey).IsUnique();
        b.Entity<Lead>().Property(x => x.Version).IsRowVersion();
        b.Entity<OutreachDraft>().Property(x => x.Version).IsRowVersion();
        b.Entity<Reply>().Property(x => x.ExternalMessageId).HasMaxLength(200);
        b.Entity<Reply>().HasIndex(x => x.ExternalMessageId).IsUnique().HasFilter("[ExternalMessageId] IS NOT NULL");
        b.Entity<FollowUp>().HasIndex(x => new { x.LeadId, x.SequenceNo }).IsUnique();
        b.Entity<OutreachDraft>().HasIndex(x => x.FollowUpId).IsUnique().HasFilter("[FollowUpId] IS NOT NULL");
        b.Entity<Lead>().HasOne<Campaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<OutreachDraft>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FollowUp>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Meeting>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reply>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Lead>().Property''',1);f.write_text(s)
Path('src/BDMAgent.Application/Services/LeadNormalizer.cs').write_text('''using System.Security.Cryptography;
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
''')
Path('.gitignore').write_text('bin/\nobj/\n.vs/\n.env\n.keys/\nartifacts/\n*.user\n')
