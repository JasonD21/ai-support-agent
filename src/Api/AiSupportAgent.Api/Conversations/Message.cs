using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Conversations;

public enum MessageRole
{
    User,
    Assistant,
    System
}

public class Message : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = default!;
    public List<Guid>? RetrievedChunkIds { get; set; }     // snapshot, not a live FK
    public List<string>? MatchedTitles { get; set; }       // display-resilient after edits
    public double? TopSimilarity { get; set; }
    public string? ModelUsed { get; set; }
    public bool? WasGrounded { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}