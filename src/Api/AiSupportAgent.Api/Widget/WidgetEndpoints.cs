using System.Diagnostics;
using System.Text;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Conversations;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Persistence;
using AiSupportAgent.Api.Rag;
using AiSupportAgent.Api.Tenancy;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiSupportAgent.Api.Widget;

public static class WidgetEndpoints
{
    public static IEndpointRouteBuilder MapWidgetEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/widget")
            .AddEndpointFilter<SiteKeyTenantFilter>()
            .RequireCors("widget")
            .RequireRateLimiting("widget");

        g.MapGet("/config", GetConfig);
        g.MapPost("/conversations", StartConversation);
        g.MapPost("/conversations/{id:guid}/messages", PostMessage);
        g.MapGet("/conversations/{id:guid}/messages", GetMessages);
        return app;
    }

    private static IResult GetConfig(HttpContext http)
    {
        var t = (Tenant)http.Items["Tenant"]!;
        var c = t.AgentConfig;
        return Results.Ok(new WidgetConfigResponse(c.AgentName, c.Greeting, c.ThemeColor, c.BubblePosition.ToString()));
    }

    private static async Task<IResult> StartConversation(HttpContext http, AppDbContext db)
    {
        var t = (Tenant)http.Items["Tenant"]!;
        var convo = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = t.Id,
            SessionToken = OpaqueToken.New(),
            Status = ConversationStatus.Active,
            OriginUrl = http.Request.Headers["X-Widget-Origin"].FirstOrDefault(),
            StartedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };
        db.Conversations.Add(convo);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.Ok(new StartConversationResponse(convo.Id, convo.SessionToken, t.AgentConfig.Greeting));
    }

    private static async Task<IResult> GetMessages(Guid id, HttpContext http, AppDbContext db)
    {
        var sessionToken = http.Request.Headers["X-Session-Token"].FirstOrDefault();
        var convo = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id, http.RequestAborted);
        if (convo is null || sessionToken is null || convo.SessionToken != sessionToken)
            return Results.Problem(statusCode: 401, title: "Invalid session.");

        var msgs = await db.Messages.Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new WidgetHistoryMessage(m.Role == MessageRole.Assistant ? "assistant" : "user", m.Content, m.CreatedAt))
            .ToListAsync(http.RequestAborted);
        return Results.Ok(msgs);
    }

    private static async Task PostMessage(Guid id, WidgetMessageRequest body, HttpContext http, AppDbContext db, RetrievalService retrieval, IChatClient chat,
        SecretProtector protector, IOptions<RagOptions> ragOpts)
    {
        var ct = http.RequestAborted;
        var resp = http.Response;
        Sse.Prepare(resp);
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var tenant = (Tenant)http.Items["Tenant"]!;
        var opts = ragOpts.Value;

        var sessionToken = http.Request.Headers["X-Session-Token"].FirstOrDefault();
        var convo = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (convo is null || sessionToken is null || convo.SessionToken != sessionToken)
        { await Sse.Send(resp, new { type = "error", message = "Invalid session." }, ct); return; }
        if (convo.Status == ConversationStatus.Closed)
        { await Sse.Send(resp, new { type = "error", message = "Conversation closed." }, ct); return; }

        var text = body.Message?.Trim() ?? "";
        if (text.Length == 0) { await Sse.Send(resp, new { type = "error", message = "Empty message." }, ct); return; }
        if (text.Length > opts.MaxInputChars) text = text[..opts.MaxInputChars];

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // persist the visitor message + meter per-tenant usage
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = convo.Id,
            Role = MessageRole.User,
            Content = text,
            CreatedAt = now
        });
        convo.LastMessageAt = now;

        var usage = await db.TenantDailyUsages.FirstOrDefaultAsync(u => u.UsageDate == today, ct);
        if (usage is null)
        {
            usage = new TenantDailyUsage { TenantId = tenant.Id, UsageDate = today, MessageCount = 0 };
            db.TenantDailyUsages.Add(usage);
        }
        usage.MessageCount++;
        await db.SaveChangesAsync(ct);

        if (usage.MessageCount > opts.PerTenantDailyMessageCap)
        { await Handoff(resp, db, convo, "QuotaOverflow", ct); return; }

        if (tenant.AgentConfig.Handoff.OnExplicitRequest && HandoffIntent.WantsHuman(text))
        { await Handoff(resp, db, convo, "Explicit", ct); return; }

        var chunks = await retrieval.RetrieveAsync(text, ct);
        var topSim = chunks.Count > 0 ? chunks.Max(c => c.Similarity) : 0;
        if (tenant.AgentConfig.Handoff.OnNoGrounding && (chunks.Count == 0 || topSim < opts.SimilarityFloor))
        { await Handoff(resp, db, convo, "NoGrounding", ct, topSim); return; }

        // global free-tier guard (shared OpenRouter key — cross-tenant blast radius)
        var global = await db.GlobalDailyUsages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(gx => gx.UsageDate == today, ct);
        if (global is null)
        {
            global = new GlobalDailyUsage { UsageDate = today, FreeCallCount = 0 };
            db.GlobalDailyUsages.Add(global);
        }
        if (global.FreeCallCount >= opts.GlobalDailyFreeCallCap)
        { await Handoff(resp, db, convo, "QuotaOverflow", ct); return; }
        global.FreeCallCount++;
        await db.SaveChangesAsync(ct);

        // history from persisted messages (sliding window; includes the message just saved)
        var prior = await db.Messages.Where(m => m.ConversationId == convo.Id)
            .OrderByDescending(m => m.CreatedAt).Take(10)
            .Select(m => new { m.Role, m.Content, m.CreatedAt })
            .ToListAsync(ct);
        prior.Reverse();

        var system = PromptBuilder.BuildSystemPrompt(tenant.AgentConfig, tenant.Name, chunks);
        var messages = new List<ChatMessage> { new("system", system) };
        foreach (var m in prior)
            messages.Add(new(m.Role == MessageRole.Assistant ? "assistant" : "user", m.Content));

        string? apiKey = null, baseUrl = null;
        var cred = await db.LlmCredentials.FirstOrDefaultAsync(ct);
        if (cred is not null) { try { apiKey = protector.Decrypt(cred.ApiKeyEncrypted); baseUrl = cred.BaseUrl; } catch { } }
        var config = new ChatClientConfig(apiKey, baseUrl, opts.Models);

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

        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = convo.Id,
            Role = MessageRole.Assistant,
            Content = full.ToString(),
            RetrievedChunkIds = chunks.Select(c => c.ChunkId).ToList(),
            MatchedTitles = chunks.Select(c => c.Title).Distinct().ToList(),
            TopSimilarity = topSim,
            ModelUsed = result.Model,
            WasGrounded = !refused,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            CreatedAt = DateTime.UtcNow
        });
        convo.LastMessageAt = DateTime.UtcNow;
        if (refused) convo.Status = ConversationStatus.HandedOff;
        await db.SaveChangesAsync(ct);

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

    private static async Task Handoff(HttpResponse resp, AppDbContext db, Conversation convo,
        string reason, CancellationToken ct, double topSim = 0)
    {
        convo.Status = ConversationStatus.HandedOff;
        await db.SaveChangesAsync(ct);
        await Sse.Send(resp, new { type = "handoff", reason }, ct);
        await Sse.Send(resp, new { type = "done", wasGrounded = false, topSimilarity = topSim }, ct);
    }
}