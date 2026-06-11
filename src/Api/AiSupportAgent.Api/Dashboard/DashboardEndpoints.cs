using AiSupportAgent.Api.Leads;
using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return Results.Ok(new
            {
                conversations = await db.Conversations.CountAsync(),
                leads = await db.Leads.CountAsync(),
                newLeads = await db.Leads.CountAsync(l => l.Status == LeadStatus.New),
                knowledgeItems = await db.KnowledgeItems.CountAsync(),
                messagesToday = await db.TenantDailyUsages.Where(u => u.UsageDate == today)
                    .Select(u => (int?)u.MessageCount).FirstOrDefaultAsync() ?? 0
            });
        }).RequireAuthorization();
        return app;
    }
}