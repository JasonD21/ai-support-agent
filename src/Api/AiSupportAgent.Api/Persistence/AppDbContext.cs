using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Conversations;
using AiSupportAgent.Api.Identity;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Leads;
using AiSupportAgent.Api.Tenancy;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    private readonly ITenantContext _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LlmCredential> LlmCredentials => Set<LlmCredential>();
    public DbSet<TenantDailyUsage> TenantDailyUsages => Set<TenantDailyUsage>();
    public DbSet<GlobalDailyUsage> GlobalDailyUsages => Set<GlobalDailyUsage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);                 // sets up the Identity tables — must be first
        b.HasPostgresExtension("vector");        // emits CREATE EXTENSION in the migration

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.SiteKey).IsUnique();
            e.HasIndex(x => x.OwnerUserId).IsUnique();
            e.Property(x => x.AgentConfig).HasJsonbConversion();
            e.Property(x => x.AllowedOrigins).HasJsonbConversion();
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<Tenant>(x => x.OwnerUserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.Id == _tenant.TenantId);   // owner sees only their tenant
        });

        b.Entity<LlmCredential>(e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique();
            e.Property(x => x.Provider).HasConversion<string>();
            e.HasOne<Tenant>().WithOne().HasForeignKey<LlmCredential>(x => x.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<TenantDailyUsage>(e =>
        {
            e.HasKey(x => new { x.TenantId, x.UsageDate });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<GlobalDailyUsage>(e => e.HasKey(x => x.UsageDate));

        b.Entity<KnowledgeItem>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ItemType });
            e.Property(x => x.ItemType).HasConversion<string>();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<Chunk>(e =>
        {
            e.HasIndex(x => x.TenantId);
            e.Property(x => x.Embedding).HasColumnType("vector(384)");
            e.HasOne<KnowledgeItem>().WithMany().HasForeignKey(x => x.KnowledgeItemId)
             .OnDelete(DeleteBehavior.Cascade);                     // delete path for chunks
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.NoAction);                    // denormalized FK, not a delete path
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<Conversation>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status, x.LastMessageAt });
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<Message>(e =>
        {
            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            e.Property(x => x.Role).HasConversion<string>();
            e.Property(x => x.RetrievedChunkIds).HasJsonbConversion();
            e.Property(x => x.MatchedTitles).HasJsonbConversion();
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<Lead>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.ConversationId).IsUnique();
            e.Property(x => x.Reason).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne<Conversation>().WithOne().HasForeignKey<Lead>(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });
    }

    public override int SaveChanges()
    {
        StampTenant();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        StampTenant();
        return base.SaveChangesAsync(ct);
    }

    // Auto-stamp TenantId on new tenant-owned rows (unless already set, e.g. by system seeding).
    private void StampTenant()
    {
        if (_tenant.TenantId is not { } tenantId) return;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = tenantId;
    }
}