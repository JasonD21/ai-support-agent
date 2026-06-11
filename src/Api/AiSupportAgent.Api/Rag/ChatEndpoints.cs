using System.Diagnostics;
using System.Text;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiSupportAgent.Api.Rag;

public static class ChatEndpoints
{
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
        Sse.Prepare(resp);
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var tenant = await db.Tenants.FirstOrDefaultAsync(ct);
        if (tenant is null) { await Sse.Send(resp, new { type = "error", message = "Tenant not found." }, ct); return; }

        var message = req.Message?.Trim() ?? "";
        if (message.Length == 0) { await Sse.Send(resp, new { type = "error", message = "Empty message." }, ct); return; }

        if (tenant.AgentConfig.Handoff.OnExplicitRequest && HandoffIntent.WantsHuman(message))
        {
            await Sse.Send(resp, new { type = "handoff", reason = "Explicit" }, ct);
            await Sse.Send(resp, new { type = "done", wasGrounded = false }, ct);
            return;
        }

        var chunks = await retrieval.RetrieveAsync(message, ct);
        var topSim = chunks.Count > 0 ? chunks.Max(c => c.Similarity) : 0;
        if (tenant.AgentConfig.Handoff.OnNoGrounding && (chunks.Count == 0 || topSim < ragOpts.Value.SimilarityFloor))
        {
            await Sse.Send(resp, new { type = "handoff", reason = "NoGrounding" }, ct);
            await Sse.Send(resp, new { type = "done", wasGrounded = false, topSimilarity = topSim }, ct);
            return;
        }

        var system = PromptBuilder.BuildSystemPrompt(tenant.AgentConfig, tenant.Name, chunks);
        var messages = new List<ChatMessage> { new("system", system) };
        foreach (var h in (req.History ?? []).TakeLast(8)) messages.Add(new(h.Role, h.Content));
        messages.Add(new("user", message));

        string? apiKey = null, baseUrl = null;
        var cred = await db.LlmCredentials.FirstOrDefaultAsync(ct);
        if (cred is not null) { try { apiKey = protector.Decrypt(cred.ApiKeyEncrypted); baseUrl = cred.BaseUrl; } catch { } }
        var config = new ChatClientConfig(apiKey, baseUrl, ragOpts.Value.Models);

        var sw = Stopwatch.StartNew();
        var full = new StringBuilder();
        var result = new ChatResult();
        try
        {
            await foreach (var token in chat.StreamAsync(messages, config, result, ct))
            {
                full.Append(token);
                await Sse.Send(resp, new { type = "token", value = token }, ct);
            }
        }
        catch { await Sse.Send(resp, new { type = "error", message = "The model is unavailable right now." }, ct); return; }

        var refused = full.ToString().Contains(PromptBuilder.InsufficientInfo, StringComparison.OrdinalIgnoreCase);
        if (refused) await Sse.Send(resp, new { type = "handoff", reason = "NoGrounding" }, ct);

        await Sse.Send(resp, new
        {
            type = "done",
            wasGrounded = !refused,
            topSimilarity = topSim,
            latencyMs = (int)sw.ElapsedMilliseconds,
            model = result.Model,
            titles = chunks.Select(c => c.Title).Distinct().ToArray()
        }, ct);
    }
}