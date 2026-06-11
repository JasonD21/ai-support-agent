using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiSupportAgent.Api.Rag;

public class OpenRouterChatClient(IHttpClientFactory httpFactory, IOptions<RagOptions> ragOptions) : IChatClient
{
    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, ChatClientConfig config, ChatResult result,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var http = httpFactory.CreateClient();
        var baseUrl = (config.BaseUrl ?? "https://openrouter.ai/api/v1").TrimEnd('/');
        var apiKey = config.ApiKey ?? ragOptions.Value.SharedApiKey;
        var models = config.Models.Count > 0 ? config.Models : ragOptions.Value.Models;

        HttpResponseMessage? response = null;
        foreach (var model in models)
        {
            var request = BuildRequest(baseUrl, apiKey, model, messages, ragOptions.Value.MaxOutputTokens);
            HttpResponseMessage resp;
            try { resp = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
            catch { continue; }
            if (resp.IsSuccessStatusCode) { response = resp; result.Model = model; break; }
            resp.Dispose();
        }
        if (response is null) throw new InvalidOperationException("All chat models failed.");

        using (response)
        await using (var stream = await response.Content.ReadAsStreamAsync(ct))
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line[5..].Trim();
                if (data.Length == 0) continue;
                if (data == "[DONE]") yield break;
                var token = ExtractDelta(data);
                if (!string.IsNullOrEmpty(token)) yield return token;
            }
        }
    }

    private static HttpRequestMessage BuildRequest(string baseUrl, string? apiKey, string model, IReadOnlyList<ChatMessage> messages, int maxTokens)
    {
        var body = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            max_tokens = maxTokens
        };
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey)) req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Headers.Add("HTTP-Referer", "https://github.com/JasonD21/ai-support-agent");
        req.Headers.Add("X-Title", "AI Support Agent");
        return req;
    }

    private static string? ExtractDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return null;
            var delta = choices[0].GetProperty("delta");
            return delta.TryGetProperty("content", out var c) ? c.GetString() : null;
        }
        catch { return null; }
    }
}