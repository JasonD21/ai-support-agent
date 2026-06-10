var builder = WebApplication.CreateBuilder(args);

const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ai-support-agent-api",
    timeUtc = DateTime.UtcNow
}));

app.Run();