namespace AiSupportAgent.Api.Rag;

public class RagOptions
{
    public string? SharedApiKey { get; set; }
    public List<string> Models { get; set; } = [];
    public double SimilarityFloor { get; set; } = 0.35;
    public int MaxOutputTokens { get; set; } = 400;
    public string? SharedBaseUrl { get; set; }   // default provider (OpenAI-compatible)
    public int MaxInputChars { get; set; } = 1000;
    public int PerTenantDailyMessageCap { get; set; } = 200;
    public int GlobalDailyFreeCallCap { get; set; } = 45;
    public int ConsecutiveUnresolvedThreshold { get; set; } = 2;
    public int OperatorAlertThreshold { get; set; } = 40;
    public string? OperatorAlertEmail { get; set; }
}