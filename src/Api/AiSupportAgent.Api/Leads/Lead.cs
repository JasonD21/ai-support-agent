using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Leads;

public enum LeadReason
{
    Explicit,
    NoGrounding,
    Unresolved,
    QuotaOverflow
}
public enum LeadStatus
{
    New,
    Contacted,
    Closed
}

public class Lead : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }               // unique — one open lead per conversation
    public LeadReason Reason { get; set; }
    public string? StumpingQuestion { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? VisitorMessage { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}