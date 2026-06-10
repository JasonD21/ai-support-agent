using System.Security.Cryptography;

namespace AiSupportAgent.Api.Tenancy;

public static class SiteKeyGenerator
{
    public static string New()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return "wsk_" + token;
    }
}