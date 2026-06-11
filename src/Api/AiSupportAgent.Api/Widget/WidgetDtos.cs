namespace AiSupportAgent.Api.Widget;

public record WidgetConfigResponse(
    string AgentName,
    string Greeting,
    string ThemeColor,
    string BubblePosition
);
public record StartConversationResponse(
    Guid ConversationId,
    string SessionToken,
    string Greeting
);
public record WidgetMessageRequest(
    string Message
);
public record WidgetHistoryMessage(
    string Role,
    string Content,
    DateTime CreatedAt
);