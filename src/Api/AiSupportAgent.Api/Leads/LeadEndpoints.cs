using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Leads;

public record UpdateLeadStatusRequest(string Status);

public static class LeadEndpoints
{
    public static IEndpointRouteBuilder MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/leads").RequireAuthorization();

        g.MapGet("", async (AppDbContext db, string? status) =>
        {
            var q = db.Leads.AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<LeadStatus>(status, true, out var s))
                q = q.Where(l => l.Status == s);
            var items = await q.OrderByDescending(l => l.CreatedAt).Select(l => new
            {
                l.Id,
                l.ConversationId,
                reason = l.Reason.ToString(),
                status = l.Status.ToString(),
                l.ContactName,
                l.ContactEmail,
                l.ContactPhone,
                l.VisitorMessage,
                l.StumpingQuestion,
                l.CreatedAt
            }).ToListAsync();
            return Results.Ok(items);
        });

        g.MapPatch("/{id:guid}/status", async (Guid id, UpdateLeadStatusRequest body, AppDbContext db) =>
        {
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id);
            if (lead is null) return Results.NotFound();
            if (!Enum.TryParse<LeadStatus>(body.Status, true, out var s)) return Results.Problem(statusCode: 400, title: "Invalid status.");
            lead.Status = s;
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        return app;
    }
}