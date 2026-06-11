using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Persistence;
using AiSupportAgent.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Widget;

// Resolves the tenant from the public site key, sets the ambient tenant context
// (so global query filters scope everything), and validates the reported host origin.
public class SiteKeyTenantFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var tenantContext = (TenantContext)http.RequestServices.GetRequiredService<ITenantContext>();

        var siteKey = http.Request.Headers["X-Site-Key"].FirstOrDefault() ?? http.Request.Query["siteKey"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(siteKey))
            return Results.Problem(statusCode: 401, title: "Missing site key.");

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.SiteKey == siteKey, http.RequestAborted);
        if (tenant is null)
            return Results.Problem(statusCode: 401, title: "Invalid site key.");

        tenantContext.SetTenant(tenant.Id);
        http.Items["Tenant"] = tenant;

        // Reported host-page origin. Honestly spoofable by a scripted client —
        // it stops casual cross-site reuse; rate limits + quotas are the real backstop.
        if (tenant.AllowedOrigins is { Count: > 0 })
        {
            var reported = http.Request.Headers["X-Widget-Origin"].FirstOrDefault()
                           ?? http.Request.Headers.Referer.FirstOrDefault();
            if (!OriginAllowed(reported, tenant.AllowedOrigins))
                return Results.Problem(statusCode: 403, title: "Origin not allowed.");
        }

        return await next(ctx);
    }

    private static bool OriginAllowed(string? reported, List<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(reported)) return false;
        if (!Uri.TryCreate(reported, UriKind.Absolute, out var u)) return false;
        var origin = $"{u.Scheme}://{u.Authority}";
        return allowed.Any(a => string.Equals(a.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}