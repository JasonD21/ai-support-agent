namespace AiSupportAgent.Api.Common;

public class DemoReadOnlyMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var method = ctx.Request.Method;
        var isMutating = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
        var path = ctx.Request.Path.Value ?? "";
        var isOwnerApi = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                         && !path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
                         && !path.StartsWith("/api/widget", StringComparison.OrdinalIgnoreCase);

        if (isMutating && isOwnerApi && ctx.User.FindFirst("is_demo")?.Value == "true")
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { title = "The demo account is read-only." });
            return;
        }
        await next(ctx);
    }
}