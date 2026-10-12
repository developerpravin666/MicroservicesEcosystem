using AccountingInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Infrastructure.Persistence;

public sealed class RegistrationEmailTemplateStore(TenantDbContext db) : IRegistrationEmailTemplateStore
{
    private static string NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return RegistrationEmailTemplate.StoreRegistrationKey;
        var trimmed = key.Trim().ToLowerInvariant().Replace('-', '_');
        return trimmed switch
        {
            "admin_approval_request" => RegistrationEmailTemplate.AdminApprovalRequestKey,
            _ => RegistrationEmailTemplate.StoreRegistrationKey
        };
    }

    public Task<RegistrationEmailTemplate> GetAsync(CancellationToken ct) =>
        GetAsync(RegistrationEmailTemplate.StoreRegistrationKey, ct);

    public async Task<RegistrationEmailTemplate> GetAsync(string key, CancellationToken ct)
    {
        var normalized = NormalizeKey(key);
        try
        {
            var rows = await db.Database.SqlQueryRaw<RegistrationEmailTemplate>(
                "SELECT subject AS \"Subject\", html_body AS \"HtmlBody\" FROM tenant.__email_templates WHERE key = {0}",
                normalized)
                .ToListAsync(ct);
            return rows.FirstOrDefault() ?? RegistrationEmailTemplate.GetDefault(normalized);
        }
        catch
        {
            return RegistrationEmailTemplate.GetDefault(normalized);
        }
    }

    public Task SaveAsync(RegistrationEmailTemplate template, CancellationToken ct) =>
        SaveAsync(RegistrationEmailTemplate.StoreRegistrationKey, template, ct);

    public async Task SaveAsync(string key, RegistrationEmailTemplate template, CancellationToken ct)
    {
        var normalized = NormalizeKey(key);
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS tenant.__email_templates (
                key varchar(100) PRIMARY KEY,
                subject text NOT NULL,
                html_body text NOT NULL
            );", ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant.__email_templates(key, subject, html_body) VALUES({normalized}, {template.Subject}, {template.HtmlBody}) ON CONFLICT(key) DO UPDATE SET subject=EXCLUDED.subject, html_body=EXCLUDED.html_body", ct);
    }

    public Task ResetAsync(CancellationToken ct) =>
        ResetAsync(RegistrationEmailTemplate.StoreRegistrationKey, ct);

    public async Task ResetAsync(string key, CancellationToken ct)
    {
        var normalized = NormalizeKey(key);
        await SaveAsync(normalized, RegistrationEmailTemplate.GetDefault(normalized), ct);
    }
}
