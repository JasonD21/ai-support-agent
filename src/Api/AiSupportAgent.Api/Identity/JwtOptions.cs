namespace AiSupportAgent.Api.Identity;

public class JwtOptions
{
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "ai-support-agent";
    public string Audience { get; set; } = "ai-support-agent";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}