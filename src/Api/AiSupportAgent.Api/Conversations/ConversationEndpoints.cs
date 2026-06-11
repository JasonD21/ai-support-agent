// Conversations/ConversationEndpoints.cs
using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Conversations;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/conversations").RequireAuthorization();

        g.MapGet("", async (AppDbContext db, int? offset, int? limit) =>
        {
            var skip = offset ?? 0;
            var take = Math.Clamp(limit ?? 25, 1, 100);
            var baseQuery = db.Conversations.OrderByDescending(c => c.LastMessageAt);
            var total = await baseQuery.CountAsync();
            var items = await baseQuery.Skip(skip).Take(take).Select(c => new
            {
                c.Id,
                status = c.Status.ToString(),
                c.StartedAt,
                c.LastMessageAt,
                messageCount = db.Messages.Count(m => m.ConversationId == c.Id),
                hasLead = db.Leads.Any(l => l.ConversationId == c.Id)
            }).ToListAsync();
            return Results.Ok(new { total, items });
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var c = await db.Conversations.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();

            var messages = await db.Messages.Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    role = m.Role.ToString(),
                    m.Content,
                    m.CreatedAt,
                    m.TopSimilarity,
                    m.WasGrounded,
                    m.ModelUsed,
                    m.LatencyMs,
                    titles = m.MatchedTitles
                }).ToListAsync();

            var lead = await db.Leads.Where(l => l.ConversationId == id)
                .Select(l => new { l.Id, reason = l.Reason.ToString(), status = l.Status.ToString(), l.ContactName, l.ContactEmail })
                .FirstOrDefaultAsync();

            return Results.Ok(new { c.Id, status = c.Status.ToString(), c.OriginUrl, c.StartedAt, c.LastMessageAt, messages, lead });
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var c = await db.Conversations.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            db.Conversations.Remove(c); // cascades messages + lead
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}