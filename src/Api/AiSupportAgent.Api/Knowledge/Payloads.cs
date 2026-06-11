namespace AiSupportAgent.Api.Knowledge;

public record FaqPayload(
    string Question,
    string Answer
);
public record ServicePayload(
    string Name,
    string Description,
    string? Price,
    string? Category
);
public record PolicyPayload(
    string Title,
    string Body
);
public record BusinessProfilePayload(
    string Name,
    string? About,
    string? Hours,
    string? Location,
    string? ContactEmail,
    string? ContactPhone,
    List<string>? Links
);