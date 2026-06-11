using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AiSupportAgent.Api.Common;
using AiSupportAgent.Api.Identity;
using AiSupportAgent.Api.Knowledge;
using AiSupportAgent.Api.Persistence;
using AiSupportAgent.Api.Rag;
using AiSupportAgent.Api.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using AiSupportAgent.Api.Widget;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;   // keep "sub"/"tenantId" claim names verbatim

var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());            // needed for the refresh cookie
    options.AddPolicy("widget", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.UseVector()
    )
);

builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    }).AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));



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

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

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

builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<IEmbedder>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Embedding");
    var root = sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
    return new OnnxEmbedder(
        Path.Combine(root, cfg["ModelPath"]!),
        Path.Combine(root, cfg["VocabPath"]!),
        cfg["ModelId"]!);
});
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AiSupportAgent.Api.Rag.IChatClient, AiSupportAgent.Api.Rag.OpenRouterChatClient>();

builder.Services.AddHttpClient();

builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<RetrievalService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
    app.MapOpenApi();                 // serves /openapi/v1.json
    app.MapScalarApiReference();      // serves the UI at /scalar
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapAgentEndpoints();
app.MapKnowledgeEndpoints();
app.MapChatEndpoints();
app.MapWidgetEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ai-support-agent-api",
    timeUtc = DateTime.UtcNow
}));

app.Run();