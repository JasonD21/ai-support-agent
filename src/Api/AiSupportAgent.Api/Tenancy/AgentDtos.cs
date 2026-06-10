namespace AiSupportAgent.Api.Tenancy;

public record HandoffPostureDto(
    bool OnExplicitRequest,
    bool OnNoGrounding,
    bool OnConsecutiveUnresolved,
    int ConsecutiveUnresolvedThreshold
);

public record AgentConfigDto(
    string AgentName,
    string Greeting,
    TonePreset Tone,
    string? ExtraInstructions,
    string ThemeColor,
    BubblePosition BubblePosition,
    HandoffPostureDto Handoff
);

public record AgentSettingsResponse(
    AgentConfigDto Config,
    string SiteKey,
    List<string> AllowedOrigins,
    string EmbedSnippet
);

public record UpdateAgentRequest(
    AgentConfigDto Config,
    List<string>? AllowedOrigins
);

public record RotateSiteKeyResponse(
    string SiteKey,
    string EmbedSnippet
);

public record LlmCredentialResponse(
    bool Configured,
    LlmProvider? Provider,
    string? BaseUrl,
    string? MaskedKey
);

public record UpsertLlmCredentialRequest(
    LlmProvider Provider,
    string? BaseUrl,
    string ApiKey
);