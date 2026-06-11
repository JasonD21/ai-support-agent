using System.Text.Json;

namespace AiSupportAgent.Api.Rag;

public static class Sse
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Prepare(HttpResponse resp)
    {
        resp.ContentType = "text/event-stream";
        resp.Headers.CacheControl = "no-cache";
        resp.Headers["X-Accel-Buffering"] = "no";
    }

    public static async Task Send(HttpResponse resp, object evt, CancellationToken ct)
    {
        await resp.WriteAsync($"data: {JsonSerializer.Serialize(evt, Json)}\n\n", ct);
        await resp.Body.FlushAsync(ct);
    }
}