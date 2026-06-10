using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Tenancy;

// Composite key (TenantId, UsageDate) — date-keyed counter, self-resets by date.
public class TenantDailyUsage : ITenantOwned
{
    public Guid TenantId { get; set; }
    public DateOnly UsageDate { get; set; }
    public int MessageCount { get; set; }
}