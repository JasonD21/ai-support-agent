using System.Text.Json;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Conversations;
using AiSupportAgent.Api.Identity;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Leads;
using AiSupportAgent.Api.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace AiSupportAgent.Api.Persistence;

public static class DemoSeeder
{
    private const string Model = "all-MiniLM-L6-v2";
    private static readonly JsonSerializerOptions Camel = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider;
        var db = svc.GetRequiredService<AppDbContext>();
        var users = svc.GetRequiredService<UserManager<ApplicationUser>>();
        var embedder = svc.GetRequiredService<IEmbedder>();
        var cfg = svc.GetRequiredService<IConfiguration>();
        var tenantCtx = (TenantContext)svc.GetRequiredService<ITenantContext>();
        var log = svc.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSeeder");

        var email = cfg["Auth:DemoEmail"] ?? "demo@demo.local";
        if (await users.Users.AnyAsync(u => u.Email == email)) return;   // idempotent

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Riverbed Coffee Roasters" };
        if (!(await users.CreateAsync(user, $"Demo!{Guid.NewGuid():N}")).Succeeded) return;  // random pw; unused

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = "Riverbed Coffee Roasters",
            SiteKey = SiteKeyGenerator.New(),
            AllowedOrigins = [],
            AgentConfig = new AgentConfig
            {
                AgentName = "Beans",
                Greeting = "Hi! Ask me about our coffee, shipping, returns, or wholesale.",
                Tone = TonePreset.Friendly,
                ExtraInstructions = "",
                ThemeColor = "#7c4a2d",
                BubblePosition = BubblePosition.BottomRight,
                Handoff = new() { OnExplicitRequest = true, OnNoGrounding = true }
            },
            CreatedAt = DateTime.UtcNow
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        tenantCtx.SetTenant(tenant.Id);

        void AddItem(KnowledgeItemType type, object payload, params string[] chunks)
        {
            var item = new KnowledgeItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ItemType = type,
                PayloadJson = JsonSerializer.Serialize(payload, Camel),
                CreatedAt = DateTime.UtcNow
            };
            db.KnowledgeItems.Add(item);
            var ord = 0;
            foreach (var t in chunks)
                db.Chunks.Add(new Chunk
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    KnowledgeItemId = item.Id,
                    Ordinal = ord++,
                    Content = t,
                    Embedding = new Vector(embedder.Embed(t)),
                    EmbeddingModel = Model,
                    CreatedAt = DateTime.UtcNow
                });
        }

        AddItem(KnowledgeItemType.Faq,
            new { question = "Do you ship internationally?", answer = "Yes — we ship worldwide. European and UK orders typically arrive in 5–8 business days." },
            "Q: Do you ship internationally? A: Yes — we ship worldwide. European and UK orders typically arrive in 5–8 business days.");
        AddItem(KnowledgeItemType.Faq,
            new { question = "What is your returns policy?", answer = "Unopened bags can be returned within 30 days for a full refund." },
            "Q: What is your returns policy? A: Unopened bags can be returned within 30 days for a full refund.");
        AddItem(KnowledgeItemType.Service,
            new { name = "Ethiopia Yirgacheffe", description = "A bright, floral single-origin with notes of jasmine and citrus. Light roast.", price = "R220 / 250g", category = "Single origin" },
            "Ethiopia Yirgacheffe — A bright, floral single-origin with notes of jasmine and citrus. Light roast. Price: R220 / 250g.");
        AddItem(KnowledgeItemType.Service,
            new { name = "House Espresso Blend", description = "A balanced, chocolatey blend built for espresso. Medium-dark roast.", price = "R180 / 250g", category = "Blend" },
            "House Espresso Blend — A balanced, chocolatey blend built for espresso. Medium-dark roast. Price: R180 / 250g.");
        AddItem(KnowledgeItemType.Policy,
            new { title = "Wholesale", body = "We supply cafés and offices. Minimum order is 5kg per month, billed monthly, with 20% off retail. Email wholesale@riverbed.example to set up an account." },
            "Wholesale: We supply cafés and offices. Minimum order is 5kg/month, billed monthly, 20% off retail. Email wholesale@riverbed.example to set up an account.");
        AddItem(KnowledgeItemType.BusinessProfile,
            new { name = "Riverbed Coffee Roasters", about = "A small-batch specialty roastery roasting since 2016.", hours = "Mon–Fri 7am–4pm, Sat 8am–1pm", location = "Cape Town, South Africa", contactEmail = "hello@riverbed.example", contactPhone = "+27 21 000 0000" },
            "About: Riverbed Coffee Roasters is a small-batch specialty roastery, roasting since 2016.",
            "Hours: Monday to Friday 7am–4pm, Saturday 8am–1pm.",
            "Location: Cape Town, South Africa.",
            "Contact: hello@riverbed.example, +27 21 000 0000.");
        await db.SaveChangesAsync();

        // a resolved conversation (shows grounded metadata in the dashboard)
        var c1 = new Conversation { Id = Guid.NewGuid(), TenantId = tenant.Id, SessionToken = "seed-1", Status = ConversationStatus.Active, StartedAt = DateTime.UtcNow.AddMinutes(-30), LastMessageAt = DateTime.UtcNow.AddMinutes(-29) };
        db.Conversations.Add(c1);
        db.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = c1.Id, TenantId = tenant.Id, Role = MessageRole.User, Content = "Do you ship to the UK?", CreatedAt = DateTime.UtcNow.AddMinutes(-30) });
        db.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = c1.Id, TenantId = tenant.Id, Role = MessageRole.Assistant, Content = "Yes! We ship worldwide, and UK orders usually arrive in 5–8 business days.", WasGrounded = true, TopSimilarity = 0.82, ModelUsed = "seed", LatencyMs = 640, MatchedTitles = new List<string> { "Do you ship internationally?" }, CreatedAt = DateTime.UtcNow.AddMinutes(-29) });

        // a handed-off conversation with a captured lead
        var c2 = new Conversation { Id = Guid.NewGuid(), TenantId = tenant.Id, SessionToken = "seed-2", Status = ConversationStatus.HandedOff, StartedAt = DateTime.UtcNow.AddMinutes(-15), LastMessageAt = DateTime.UtcNow.AddMinutes(-14) };
        db.Conversations.Add(c2);
        db.Messages.Add(new Message { Id = Guid.NewGuid(), ConversationId = c2.Id, TenantId = tenant.Id, Role = MessageRole.User, Content = "Can you make a custom blend for my wedding?", CreatedAt = DateTime.UtcNow.AddMinutes(-15) });
        db.Leads.Add(new Lead { Id = Guid.NewGuid(), TenantId = tenant.Id, ConversationId = c2.Id, Reason = LeadReason.NoGrounding, StumpingQuestion = "Can you make a custom blend for my wedding?", ContactName = "Sam Visitor", ContactEmail = "sam@example.com", VisitorMessage = "Looking for 40 bags with custom labels.", Status = LeadStatus.New, CreatedAt = DateTime.UtcNow.AddMinutes(-14) });
        await db.SaveChangesAsync();

        log.LogInformation("Seeded demo tenant. SITE KEY = {SiteKey}", tenant.SiteKey);
    }
}