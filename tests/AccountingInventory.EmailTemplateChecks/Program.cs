using System.Text.Json;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Tenants.Commands.RegisterTenant;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repository, "src/AccountingInventory/AccountingInventory.Api/appsettings.json")));
var connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("AccountingInventoryDb").GetString()!;
if (!args.Contains("--local") || new NpgsqlConnectionStringBuilder(connectionString).Host is not ("localhost" or "127.0.0.1"))
    throw new InvalidOperationException("Use --local with a local PostgreSQL host.");
var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;
await using var db = new TenantDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync();
try
{
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS tenant.__email_templates (key varchar(100) PRIMARY KEY, subject text NOT NULL, html_body text NOT NULL); DELETE FROM tenant.__email_templates WHERE key='store_registration';");
    var store = new RegistrationEmailTemplateStore(db);
    if (await store.GetAsync(default) != RegistrationEmailTemplate.Default) throw new Exception("Default format changed.");
    var custom = new RegistrationEmailTemplate("Welcome {{StoreName}}", "<p>Hello {{OwnerName}}: {{StoreName}} / {{StoreSlug}}</p>");
    await store.SaveAsync(custom, default);
    if (await new RegistrationEmailTemplateStore(db).GetAsync(default) != custom) throw new Exception("Template was not persisted.");
    if (RegistrationEmailTemplateRenderer.Validate(new("Bad\nSubject", "body")) is null || RegistrationEmailTemplateRenderer.Validate(new("Hi", "{{Unknown}}")) is null)
        throw new Exception("Invalid templates must be rejected.");
    var sender = new CapturingEmailSender();
    var handler = new RegisterTenantCommandHandler(db, sender, store, new ConfigurationBuilder().Build(), null!, NullLogger<RegisterTenantCommandHandler>.Instance);
    await handler.Handle(new("Store & Electronics", "emailcheck" + Guid.NewGuid().ToString("N"), "<Owner>", "owner@example.com"), default);
    if (sender.Messages.Count != 2 || sender.Messages[0].Subject != "Welcome Store & Electronics"
        || !sender.Messages[0].Body.Contains("&lt;Owner&gt;") || !sender.Messages[0].Body.Contains("Store &amp; Electronics"))
        throw new Exception("Registration did not use the saved format with HTML-encoded customer values.");
    Console.WriteLine("PASS: default format, database persistence, validation and registration rendering. No email was sent.");
}
finally { await transaction.RollbackAsync(); }

sealed class CapturingEmailSender : IEmailSender
{
    public List<(string Subject, string Body)> Messages { get; } = [];
    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken) { Messages.Add((subject, htmlBody)); return Task.CompletedTask; }
    public Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken) => Task.FromResult(false);
}
