using BDMAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace BDMAgent.Infrastructure.Data;
public sealed class BdmDbContext(DbContextOptions<BdmDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<OutreachDraft> OutreachDrafts => Set<OutreachDraft>();
    public DbSet<AgentJob> AgentJobs => Set<AgentJob>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IntegrationAccount> IntegrationAccounts => Set<IntegrationAccount>();
    public DbSet<Suppression> Suppressions => Set<Suppression>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<IntegrationAccount>().HasKey(x => x.Id);
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
        b.Entity<Lead>().Property(x => x.Company).HasMaxLength(200).IsRequired();
        b.Entity<Lead>().HasIndex(x => new { x.Status, x.Score });
        b.Entity<Lead>().HasIndex(x => x.ContactEmail);
        b.Entity<OutreachDraft>().HasIndex(x => new { x.LeadId, x.Status });
        b.Entity<FollowUp>().HasIndex(x => new { x.Status, x.DueUtc });
        b.Entity<Reply>().HasIndex(x => x.ExternalMessageId);
        b.Entity<Campaign>().Property(x => x.Name).HasMaxLength(200);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken=default)
    {
        ChangeTracker.DetectChanges();
        foreach(var entry in ChangeTracker.Entries().Where(e=>e.Entity is not AuditLog && e.Entity is not IntegrationAccount && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            var key=entry.Metadata.FindPrimaryKey();
            AuditLogs.Add(new AuditLog{Action=entry.State.ToString(),EntityType=entry.Metadata.ClrType.Name,EntityId=key is null?null:string.Join(",",key.Properties.Select(p=>entry.Property(p.Name).CurrentValue)),Details=string.Join(",",entry.Properties.Where(p=>p.IsModified).Select(p=>p.Metadata.Name))});
        }
        return base.SaveChangesAsync(cancellationToken);
    }

}
