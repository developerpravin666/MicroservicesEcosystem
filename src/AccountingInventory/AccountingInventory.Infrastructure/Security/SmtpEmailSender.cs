using System.Net;
using System.Net.Mail;
using AccountingInventory.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Infrastructure.Security;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        await SendCoreAsync(recipient, subject, htmlBody, cancellationToken, allowFallback: true);
    }

    public Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken) =>
        SendCoreAsync(recipient, "Siddhi Mobile test email",
            "<p>Your existing SMTP email settings successfully sent this test email.</p>",
            cancellationToken, allowFallback: false);

    private async Task<bool> SendCoreAsync(string recipient, string subject, string htmlBody,
        CancellationToken cancellationToken, bool allowFallback)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var section = configuration.GetSection("Email");
        var host = section["SmtpHost"];
        var from = section["From"] ?? "noreply@siddhi-mobile.local";
        var isOffline = bool.TryParse(section["OfflineMode"], out var offline) && offline
            || bool.TryParse(configuration["Deployment:OfflineMode"], out var depOffline) && depOffline;

        if (isOffline || string.IsNullOrWhiteSpace(host) || host.Equals("offline", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("\n================== [OFFLINE EMAIL DISPATCH] ==================\n" +
                                  "To: {Recipient}\nSubject: {Subject}\n\n{HtmlBody}\n" +
                                  "==============================================================",
                                  recipient, subject, htmlBody);
            return false;
        }

        try
        {
            using var client = new SmtpClient(host, int.TryParse(section["SmtpPort"], out var port) ? port : 587)
            {
                EnableSsl = !bool.TryParse(section["EnableSsl"], out var ssl) || ssl,
                Credentials = string.IsNullOrWhiteSpace(section["Username"])
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(section["Username"], section["Password"]),
                Timeout = 10000
            };
            using var message = new MailMessage(from, recipient, subject, htmlBody) { IsBodyHtml = true };
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex) when (allowFallback && ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send email to {Recipient} via SMTP. Falling back to offline log.", recipient);
            logger.LogInformation("\n================== [OFFLINE EMAIL FALLBACK] ==================\n" +
                                  "To: {Recipient}\nSubject: {Subject}\n\n{HtmlBody}\n" +
                                  "==============================================================",
                                  recipient, subject, htmlBody);
            return false;
        }
    }
}
