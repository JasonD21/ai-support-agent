namespace AiSupportAgent.Api.Tenancy;

public enum TonePreset
{
    Friendly,
    Professional,
    Concise
}
public enum BubblePosition
{
    BottomRight,
    BottomLeft
}

// Stored as typed jsonb on Tenant. Owner-facing settings only.
public class AgentConfig
{
    public string AgentName { get; set; } = "Support Assistant";
    public string Greeting { get; set; } = "Hi! How can I help you today?";
    public TonePreset Tone { get; set; } = TonePreset.Friendly;
    public string? ExtraInstructions { get; set; }
    public string ThemeColor { get; set; } = "#4f46e5";
    public BubblePosition BubblePosition { get; set; } = BubblePosition.BottomRight;
    public HandoffPosture Handoff { get; set; } = new();
}

public class HandoffPosture
{
    public bool OnExplicitRequest { get; set; } = true;
    public bool OnNoGrounding { get; set; } = true;
    public bool OnConsecutiveUnresolved { get; set; } = true;
    public int ConsecutiveUnresolvedThreshold { get; set; } = 2;
}