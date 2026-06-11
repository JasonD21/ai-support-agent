using Microsoft.Extensions.Options;

namespace AiSupportAgent.Api.Common;

public class EmailOptions
{
    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = "jasondavids54@gmail.com";
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct);
}

public class ResendEmailSender(IHttpClientFactory httpFactory, IOptions<EmailOptions> opts, ILogger<ResendEmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var key = opts.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) { log.LogWarning("Resend not configured; skipping email to {To}.", to); return; }

        var http = httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from = opts.Value.FromEmail, to, subject, html })
        };
        req.Headers.Add("Authorization", $"Bearer {key}");
        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            log.LogError("Resend failed ({Status}): {Body}", resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
    }
}