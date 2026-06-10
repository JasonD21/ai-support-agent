using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Knowledge;

public enum KnowledgeItemType
{
    Faq,
    Service,
    Policy,
    BusinessProfile
}

public class KnowledgeItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public KnowledgeItemType ItemType { get; set; }
    public string PayloadJson { get; set; } = "{}";   // jsonb; typed payload records arrive in Chunk 5
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}