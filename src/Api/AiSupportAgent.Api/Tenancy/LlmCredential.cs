using AiSupportAgent.Api.Common;

namespace AiSupportAgent.Api.Tenancy;

public enum LlmProvider
{
    OpenRouter,
    OpenAI,
    Anthropic,
    Other
}

public class LlmCredential : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public LlmProvider Provider { get; set; }
    public string? BaseUrl { get; set; }
    public byte[] ApiKeyEncrypted { get; set; } = default!;   // encrypted via Data Protection (Chunk 4)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}