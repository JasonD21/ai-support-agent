namespace AiSupportAgent.Api.Rag;

public class RagOptions
{
    public string? SharedApiKey { get; set; }
    public List<string> Models { get; set; } = [];
    public double SimilarityFloor { get; set; } = 0.35;
    public int MaxOutputTokens { get; set; } = 400;
    public string? SharedBaseUrl { get; set; }   // default provider (OpenAI-compatible)
}