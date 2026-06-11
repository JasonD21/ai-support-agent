// Rag/IChatClient.cs  (replace)
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
public sealed class ChatResult { public string? Model { get; set; } }

public interface IChatClient
{
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages, ChatClientConfig config, ChatResult result, CancellationToken ct);
}