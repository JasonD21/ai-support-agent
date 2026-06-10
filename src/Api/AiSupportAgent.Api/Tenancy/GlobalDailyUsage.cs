namespace AiSupportAgent.Api.Tenancy;

// No tenant — the shared OpenRouter free-tier guard + operator-alert debounce.
public class GlobalDailyUsage
{
    public DateOnly UsageDate { get; set; }       // PK
    public int FreeCallCount { get; set; }
    public DateTime? OperatorAlertSentAt { get; set; }
}