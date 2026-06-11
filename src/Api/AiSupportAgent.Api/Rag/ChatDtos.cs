namespace AiSupportAgent.Api.Rag;

public record PreviewHistoryItem(
    string Role,
    string Content
);   // role: "user" | "assistant"
public record PreviewChatRequest(
    string Message,
    List<PreviewHistoryItem>? History
);