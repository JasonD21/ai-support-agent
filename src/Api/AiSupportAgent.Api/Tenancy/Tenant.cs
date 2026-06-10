using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Tenancy;

// Tenant is NOT ITenantOwned — its own Id *is* the tenant id (filtered on Id).
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string SiteKey { get; set; } = default!;          // publishable, e.g. "wsk_..."
    public List<string> AllowedOrigins { get; set; } = new();
    public AgentConfig AgentConfig { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}