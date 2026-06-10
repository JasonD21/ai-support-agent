using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Conversations;

public enum ConversationStatus
{
    Active,
    HandedOff,
    Closed
}

public class Conversation : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SessionToken { get; set; } = default!;   // opaque capability token (Chunk 7)
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public string? OriginUrl { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
}