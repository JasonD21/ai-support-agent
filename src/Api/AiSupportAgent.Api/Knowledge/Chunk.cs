using AiSupportAgent.Api.Common;
using Pgvector;

namespace AiSupportAgent.Api.Knowledge;

public class Chunk : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid KnowledgeItemId { get; set; }
    public int Ordinal { get; set; }
    public string Content { get; set; } = default!;
    public Vector Embedding { get; set; } = default!;   // vector(384)
    public string EmbeddingModel { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}