using AiSupportAgent.Api.Persistence;
using AiSupportAgent.Api.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Identity;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Register(RegisterRequest req, UserManager<ApplicationUser> users, AppDbContext db,
        TokenService tokens, IWebHostEnvironment env, HttpContext http)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.BusinessName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["request"] = ["Email, password, and business name are required."] });

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, DisplayName = req.DisplayName };
        var created = await users.CreateAsync(user, req.Password);
        if (!created.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            { ["registration"] = created.Errors.Select(e => e.Description).ToArray() });

        var tenant = new Tenant
        {
            OwnerUserId = user.Id,
            Name = req.BusinessName,
            SiteKey = SiteKeyGenerator.New(),
            AgentConfig = new AgentConfig()
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return await IssueTokens(user, tenant, db, tokens, env, http);
    }

    private static async Task<IResult> Login(LoginRequest req, UserManager<ApplicationUser> users, AppDbContext db,
        TokenService tokens, IWebHostEnvironment env, HttpContext http)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null || !await users.CheckPasswordAsync(user, req.Password))
            return Results.Problem(statusCode: 401, title: "Invalid email or password.");

        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.OwnerUserId == user.Id);
        if (tenant is null)
            return Results.Problem(statusCode: 500, title: "Account is missing its tenant.");

        return await IssueTokens(user, tenant, db, tokens, env, http);
    }

    private static async Task<IResult> Refresh(HttpContext http, AppDbContext db, UserManager<ApplicationUser> users,
        TokenService tokens, IWebHostEnvironment env)
    {
        var raw = http.Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(raw))
            return Results.Problem(statusCode: 401, title: "No refresh token.");

        var hash = TokenService.Hash(raw);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt < DateTime.UtcNow)
            return Results.Problem(statusCode: 401, title: "Invalid or expired refresh token.");

        var user = await users.FindByIdAsync(existing.UserId);
        if (user is null) return Results.Problem(statusCode: 401, title: "User not found.");

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.OwnerUserId == user.Id);

        // rotate
        var (rawNew, hashNew, refreshExp) = tokens.CreateRefreshToken();
        var replacement = new RefreshToken { UserId = user.Id, TokenHash = hashNew, ExpiresAt = refreshExp };
        db.RefreshTokens.Add(replacement);
        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenId = replacement.Id;
        await db.SaveChangesAsync();

        var (access, accessExp) = tokens.CreateAccessToken(user, tenant.Id);
        http.Response.Cookies.Append("refresh_token", rawNew, RefreshCookieOptions(env, refreshExp));

        return Results.Ok(new AuthResponse(access, accessExp,
            new UserDto(user.Email!, user.DisplayName),
            new TenantDto(tenant.Id, tenant.Name, tenant.SiteKey)));
    }

    private static async Task<IResult> Logout(HttpContext http, AppDbContext db, IWebHostEnvironment env)
    {
        var raw = http.Request.Cookies["refresh_token"];
        if (!string.IsNullOrEmpty(raw))
        {
            var hash = TokenService.Hash(raw);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        http.Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            Path = "/api/auth",
            Secure = !env.IsDevelopment(),
            SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None
        });
        return Results.NoContent();
    }

    private static async Task<IResult> Me(HttpContext http, AppDbContext db, UserManager<ApplicationUser> users)
    {
        var userId = http.User.FindFirst("sub")?.Value;
        if (userId is null) return Results.Unauthorized();

        var user = await users.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();

        // tenant context was set by the middleware from the JWT → the global filter returns this owner's tenant
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null) return Results.Problem(statusCode: 500, title: "Tenant not found.");

        return Results.Ok(new MeResponse(user.Email!, user.DisplayName, tenant.Id, tenant.Name, tenant.SiteKey));
    }

    private static async Task<IResult> IssueTokens(ApplicationUser user, Tenant tenant, AppDbContext db,
        TokenService tokens, IWebHostEnvironment env, HttpContext http)
    {
        var (access, accessExp) = tokens.CreateAccessToken(user, tenant.Id);
        var (rawRefresh, refreshHash, refreshExp) = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = refreshHash, ExpiresAt = refreshExp });
        await db.SaveChangesAsync();

        http.Response.Cookies.Append("refresh_token", rawRefresh, RefreshCookieOptions(env, refreshExp));

        return Results.Ok(new AuthResponse(access, accessExp,
            new UserDto(user.Email!, user.DisplayName),
            new TenantDto(tenant.Id, tenant.Name, tenant.SiteKey))
        );
    }

    private static CookieOptions RefreshCookieOptions(IWebHostEnvironment env, DateTime expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),                                       // prod: https-only
        SameSite = env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None, // dev localhost is same-site
        Expires = expires,
        Path = "/api/auth",
        IsEssential = true
    };
}