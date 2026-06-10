using System.Text.RegularExpressions;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiSupportAgent.Api.Tenancy;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agent").RequireAuthorization().WithTags("Agent");

        group.MapGet("", GetAgent);
        group.MapPut("", UpdateAgent);
        group.MapPost("/rotate-site-key", RotateSiteKey);

        group.MapGet("/llm-credential", GetLlmCredential);
        group.MapPut("/llm-credential", UpsertLlmCredential);
        group.MapDelete("/llm-credential", DeleteLlmCredential);

        return app;
    }

    private static async Task<IResult> GetAgent(AppDbContext db, IConfiguration config)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null) return Results.Problem(statusCode: 404, title: "Tenant not found.");

        return Results.Ok(new AgentSettingsResponse(
            ToDto(tenant.AgentConfig), tenant.SiteKey, tenant.AllowedOrigins,
            BuildEmbedSnippet(WidgetBaseUrl(config), tenant.SiteKey)));
    }

    private static async Task<IResult> UpdateAgent(UpdateAgentRequest req, AppDbContext db, IConfiguration config)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null) return Results.Problem(statusCode: 404, title: "Tenant not found.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Config.AgentName)) errors["agentName"] = ["Agent name is required."];
        if (string.IsNullOrWhiteSpace(req.Config.Greeting)) errors["greeting"] = ["Greeting is required."];
        if (!IsHexColor(req.Config.ThemeColor)) errors["themeColor"] = ["Use a hex colour like #4f46e5."];
        if (req.Config.Handoff.ConsecutiveUnresolvedThreshold is < 1 or > 10)
            errors["threshold"] = ["Threshold must be between 1 and 10."];

        var origins = new List<string>();
        foreach (var o in req.AllowedOrigins ?? [])
        {
            if (TryNormalizeOrigin(o, out var norm)) origins.Add(norm);
            else errors[$"origin:{o}"] = [$"'{o}' is not a valid origin (e.g. https://example.com)."];
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        tenant.AgentConfig = FromDto(req.Config);             // reassign → guaranteed change detection
        tenant.AllowedOrigins = origins.Distinct().ToList();
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new AgentSettingsResponse(
            ToDto(tenant.AgentConfig), tenant.SiteKey, tenant.AllowedOrigins,
            BuildEmbedSnippet(WidgetBaseUrl(config), tenant.SiteKey)));
    }

    private static async Task<IResult> RotateSiteKey(AppDbContext db, IConfiguration config)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null) return Results.Problem(statusCode: 404, title: "Tenant not found.");

        tenant.SiteKey = SiteKeyGenerator.New();
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new RotateSiteKeyResponse(
            tenant.SiteKey, BuildEmbedSnippet(WidgetBaseUrl(config), tenant.SiteKey)));
    }

    private static async Task<IResult> GetLlmCredential(AppDbContext db, SecretProtector protector)
    {
        var cred = await db.LlmCredentials.FirstOrDefaultAsync();
        if (cred is null) return Results.Ok(new LlmCredentialResponse(false, null, null, null));

        string masked;
        try { masked = Mask(protector.Decrypt(cred.ApiKeyEncrypted)); }
        catch { masked = "••••"; }   // decryption failure (e.g. key loss) shouldn't 500 a read
        return Results.Ok(new LlmCredentialResponse(true, cred.Provider, cred.BaseUrl, masked));
    }

    private static async Task<IResult> UpsertLlmCredential(
        UpsertLlmCredentialRequest req, AppDbContext db, SecretProtector protector)
    {
        if (string.IsNullOrWhiteSpace(req.ApiKey))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["apiKey"] = ["API key is required."] });

        var key = req.ApiKey.Trim();
        var cred = await db.LlmCredentials.FirstOrDefaultAsync();
        if (cred is null)
        {
            cred = new LlmCredential { Provider = req.Provider, BaseUrl = req.BaseUrl, ApiKeyEncrypted = protector.Encrypt(key) };
            db.LlmCredentials.Add(cred);   // TenantId auto-stamped on SaveChanges
        }
        else
        {
            cred.Provider = req.Provider;
            cred.BaseUrl = req.BaseUrl;
            cred.ApiKeyEncrypted = protector.Encrypt(key);
            cred.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Results.Ok(new LlmCredentialResponse(true, cred.Provider, cred.BaseUrl, Mask(key)));
    }

    private static async Task<IResult> DeleteLlmCredential(AppDbContext db)
    {
        var cred = await db.LlmCredentials.FirstOrDefaultAsync();
        if (cred is not null) { db.LlmCredentials.Remove(cred); await db.SaveChangesAsync(); }
        return Results.NoContent();
    }

    // ---- helpers ----
    private static string WidgetBaseUrl(IConfiguration config) =>
        config["App:WidgetBaseUrl"]?.TrimEnd('/') ?? "http://localhost:3000";

    private static string BuildEmbedSnippet(string baseUrl, string siteKey) =>
        $"<script src=\"{baseUrl}/widget.js\" data-site-key=\"{siteKey}\" async></script>";

    private static string Mask(string s) => s.Length <= 4 ? "••••" : "••••" + s[^4..];

    private static bool IsHexColor(string s) =>
        !string.IsNullOrEmpty(s) && Regex.IsMatch(s, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

    private static bool TryNormalizeOrigin(string input, out string normalized)
    {
        normalized = "";
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "http" && uri.Scheme != "https") return false;
        normalized = uri.IsDefaultPort ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        return true;
    }

    private static AgentConfigDto ToDto(AgentConfig c) => new(
        c.AgentName, c.Greeting, c.Tone, c.ExtraInstructions, c.ThemeColor, c.BubblePosition,
        new HandoffPostureDto(c.Handoff.OnExplicitRequest, c.Handoff.OnNoGrounding,
            c.Handoff.OnConsecutiveUnresolved, c.Handoff.ConsecutiveUnresolvedThreshold));

    private static AgentConfig FromDto(AgentConfigDto d) => new()
    {
        AgentName = d.AgentName,
        Greeting = d.Greeting,
        Tone = d.Tone,
        ExtraInstructions = d.ExtraInstructions,
        ThemeColor = d.ThemeColor,
        BubblePosition = d.BubblePosition,
        Handoff = new HandoffPosture
        {
            OnExplicitRequest = d.Handoff.OnExplicitRequest,
            OnNoGrounding = d.Handoff.OnNoGrounding,
            OnConsecutiveUnresolved = d.Handoff.OnConsecutiveUnresolved,
            ConsecutiveUnresolvedThreshold = d.Handoff.ConsecutiveUnresolvedThreshold
        }
    };
}