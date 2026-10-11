using Npgsql;

namespace AccountingInventory.Api;

internal static class ProductionConfiguration
{
    public static void Validate(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment()) return;
        var config = builder.Configuration;
        var isOffline = bool.TryParse(config["Deployment:OfflineMode"], out var off) && off
            || bool.TryParse(config["Email:OfflineMode"], out var emailOff) && emailOff;
        if (isOffline) return;
        var signingKey = config["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32
            || signingKey.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || signingKey.Contains("development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Configure a unique production Jwt:SigningKey of at least 32 characters through the secret store.");
        if (!Uri.TryCreate(config["Frontend:BaseUrl"], UriKind.Absolute, out var frontend) || frontend.Scheme != "https")
            throw new InvalidOperationException("Production Frontend:BaseUrl must be an HTTPS URL.");
        if (string.IsNullOrWhiteSpace(config["Email:SmtpHost"]) || string.IsNullOrWhiteSpace(config["Email:From"])
            || (bool.TryParse(config["Email:EnableSsl"], out var smtpTls) && !smtpTls))
            throw new InvalidOperationException("Production SMTP and TLS must be configured for invitations and password recovery.");
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("AccountingInventoryDb")
            ?? throw new InvalidOperationException("Configure the production database connection through the secret store."));
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") && connection.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
            throw new InvalidOperationException("Remote production database connections must require TLS.");
    }
}
