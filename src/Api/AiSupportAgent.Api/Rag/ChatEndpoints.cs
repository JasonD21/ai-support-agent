using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiSupportAgent.Api.Rag;

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/agent/preview-chat", PreviewChat).RequireAuthorization();
        return app;
    }

    private static async Task PreviewChat(PreviewChatRequest req, HttpContext http, AppDbContext db, RetrievalService retrieval, IChatClient chat,
        SecretProtector protector, IOptions<RagOptions> ragOpts)
    {
        var ct = http.RequestAborted;
        var resp = http.Response;
        resp.ContentType = "text/event-stream";
        resp.Headers.CacheControl = "no-cache";
        resp.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        async Task Send(object evt)
        {
            await resp.WriteAsync($"data: {JsonSerializer.Serialize(evt, Json)}\n\n", ct);
            await resp.Body.FlushAsync(ct);
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(ct);
        if (tenant is null) { await Send(new { type = "error", message = "Tenant not found." }); return; }

        var message = req.Message?.Trim() ?? "";
        if (message.Length == 0) { await Send(new { type = "error", message = "Empty message." }); return; }

        // 1. explicit handoff intent
        if (tenant.AgentConfig.Handoff.OnExplicitRequest && WantsHuman(message))
        {
            await Send(new { type = "handoff", reason = "Explicit" });
            await Send(new { type = "done", wasGrounded = false });
            return;
        }

        // 2. retrieve + confidence floor
        var chunks = await retrieval.RetrieveAsync(message, ct);
        var topSim = chunks.Count > 0 ? chunks.Max(c => c.Similarity) : 0;
        if (tenant.AgentConfig.Handoff.OnNoGrounding && (chunks.Count == 0 || topSim < ragOpts.Value.SimilarityFloor))
        {
            await Send(new { type = "handoff", reason = "NoGrounding" });
            await Send(new { type = "done", wasGrounded = false, topSimilarity = topSim });
            return;
        }

        // 3. assemble + stream
        var system = PromptBuilder.BuildSystemPrompt(tenant.AgentConfig, tenant.Name, chunks);
        var messages = new List<ChatMessage> { new("system", system) };
        foreach (var h in (req.History ?? []).TakeLast(8)) messages.Add(new(h.Role, h.Content));
        messages.Add(new("user", message));

        // per-tenant BYOK
        string? apiKey = null, baseUrl = null;
        var cred = await db.LlmCredentials.FirstOrDefaultAsync(ct);
        if (cred is not null) { try { apiKey = protector.Decrypt(cred.ApiKeyEncrypted); baseUrl = cred.BaseUrl; } catch { /* fall back to shared */ } }
        var config = new ChatClientConfig(apiKey, baseUrl, ragOpts.Value.Models);

        var sw = Stopwatch.StartNew();
        var full = new StringBuilder();
        try
        {
            await foreach (var token in chat.StreamAsync(messages, config, ct))
            {
                full.Append(token);
                await Send(new { type = "token", value = token });
            }
        }
        catch
        {
            await Send(new { type = "error", message = "The model is unavailable right now." });
            return;
        }

        // post-check: did the model produce the mandated refusal?
        var refused = full.ToString().Contains(PromptBuilder.InsufficientInfo, StringComparison.OrdinalIgnoreCase);
        if (refused) await Send(new { type = "handoff", reason = "NoGrounding" });

        await Send(new
        {
            type = "done",
            wasGrounded = !refused,
            topSimilarity = topSim,
            latencyMs = (int)sw.ElapsedMilliseconds,
            titles = chunks.Select(c => c.Title).Distinct().ToArray()
        });
    }

    private static bool WantsHuman(string msg)
    {
        var l = msg.ToLowerInvariant();
        return l.Contains("talk to a human") || l.Contains("speak to a human")
            || l.Contains("speak to someone") || l.Contains("real person")
            || l.Contains("customer service") || l.Contains("talk to an agent")
            || (l.Contains("human") && (l.Contains("talk") || l.Contains("speak") || l.Contains("connect")));
    }
}