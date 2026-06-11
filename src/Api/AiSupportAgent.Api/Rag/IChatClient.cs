namespace AiSupportAgent.Api.Rag;

public record ChatMessage(
    string Role,
    string Content
);
public record ChatClientConfig(
    string? ApiKey,
    string? BaseUrl,
    IReadOnlyList<string> Models
);

public interface IChatClient
{
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, ChatClientConfig config, CancellationToken ct);
}