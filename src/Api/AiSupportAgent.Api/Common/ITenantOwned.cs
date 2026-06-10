namespace AiSupportAgent.Api.Common;

// Every tenant-owned entity implements this — drives the global filter + auto-stamp uniformly.
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}