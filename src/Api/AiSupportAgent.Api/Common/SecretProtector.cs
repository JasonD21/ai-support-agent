using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace AiSupportAgent.Api.Common;

// Encrypts BYOK keys at rest using the Data Protection key ring (persisted to Postgres in Chunk 2,
// so ciphertext survives redeploys).
public class SecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("AiSupportAgent.LlmCredential.v1");

    public byte[] Encrypt(string plaintext) => _protector.Protect(Encoding.UTF8.GetBytes(plaintext));
    public string Decrypt(byte[] ciphertext) => Encoding.UTF8.GetString(_protector.Unprotect(ciphertext));
}