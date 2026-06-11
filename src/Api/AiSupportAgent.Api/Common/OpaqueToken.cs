using System.Security.Cryptography;

namespace AiSupportAgent.Api.Common;

public static class OpaqueToken
{
    public static string New()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}