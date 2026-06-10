namespace AiSupportAgent.Api.Common;

// Runs after authentication: reads the tenantId claim from the JWT and populates ITenantContext.
// This is where the §2 multi-tenant plumbing finally gets its teeth.
public class TenantResolutionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var claim = context.User.FindFirst("tenantId")?.Value;
        if (Guid.TryParse(claim, out var tenantId))
            tenant.SetTenant(tenantId);

        await _next(context);
    }
}