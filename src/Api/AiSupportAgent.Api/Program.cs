using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Conversations;
using AiSupportAgent.Api.Dashboard;
using AiSupportAgent.Api.Identity;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Leads;
using AiSupportAgent.Api.Persistence;
using AiSupportAgent.Api.Rag;
using AiSupportAgent.Api.Tenancy;
using AiSupportAgent.Api.Widget;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;   // keep "sub"/"tenantId" claim names verbatim

var builder = WebApplication.CreateBuilder(args);

// ── CORS ────────────────────────────────────────────────────────────────────
// Default policy = the dashboard SPA (config-driven origin, credentialed for the
// refresh cookie). The widget overrides this per-endpoint with its own open policy.
var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .WithOrigins(frontendOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
    options.AddPolicy("widget", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.UseVector()));

// ── Data Protection (encrypts BYOK secrets; key ring persisted to Postgres) ──
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("ai-support-agent");

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// ── Options ───────────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ── Authentication / Authorization ────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ── JSON / OpenAPI ────────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var bearer = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Paste your access token (Scalar adds the 'Bearer ' prefix)."
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = bearer;
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
        return Task.CompletedTask;
    });
});

// ── Rate limiting (public widget surface) ────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("widget", http =>
    {
        var siteKey = http.Request.Headers["X-Site-Key"].FirstOrDefault() ?? "anon";
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "noip";
        return RateLimitPartition.GetFixedWindowLimiter($"{siteKey}:{ip}", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IChatClient, OpenRouterChatClient>();
builder.Services.AddSingleton<IEmailSender, ResendEmailSender>();
builder.Services.AddSingleton<IEmbedder>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Embedding");
    var root = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
    return new OnnxEmbedder(
        Path.Combine(root, cfg["ModelPath"]!),
        Path.Combine(root, cfg["VocabPath"]!),
        cfg["ModelId"]!);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<RetrievalService>();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseForwardedHeaders();   // first — correct scheme + client IP behind Render's proxy
app.UseCors();               // applies the default (dashboard) policy in every environment

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();             // /openapi/v1.json
    app.MapScalarApiReference();  // /scalar
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<DemoReadOnlyMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapAgentEndpoints();
app.MapKnowledgeEndpoints();
app.MapChatEndpoints();
app.MapWidgetEndpoints();
app.MapConversationEndpoints();
app.MapLeadEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ai-support-agent-api",
    timeUtc = DateTime.UtcNow
}));

// ── Startup tasks ─────────────────────────────────────────────────────────────
try { await DemoSeeder.SeedAsync(app.Services); }
catch (Exception ex) { app.Logger.LogError(ex, "Demo seed failed; continuing startup."); }

app.Run();