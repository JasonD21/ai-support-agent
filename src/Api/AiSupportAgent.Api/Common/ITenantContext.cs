namespace AiSupportAgent.Api.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }      // null = system/unscoped (no tenant resolved yet)
    void SetTenant(Guid tenantId);
}

// Scoped per request. In Chunk 3 the auth middleware calls SetTenant() after
// resolving the owner's tenant from the JWT; in Chunk 7 from the site key.
// Until then it stays null (system mode), which is expected.
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}