using System.Text.Json;
using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Domain.Entities;
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
    var sender = new CapturingEmailSender();
    var handler = new RegisterTenantCommandHandler(db, sender, new TestTemplateStore(), new ConfigurationBuilder().Build(), null!, NullLogger<RegisterTenantCommandHandler>.Instance);
    var suffix = Guid.NewGuid().ToString("N");
    var slug = "emailcheck" + suffix;
    var email = "owner" + suffix + "@example.com";
    await handler.Handle(new("Store & Electronics", slug, "<Owner>", email), default);
    foreach (var duplicate in new[] {
        new RegisterTenantCommand("Other store", slug.ToUpperInvariant(), "Owner", "other" + email),
        new RegisterTenantCommand("Other store", "other" + suffix, "Owner", "  " + email.ToUpperInvariant() + "  ") })
    {
        try { await handler.Handle(duplicate, default); throw new Exception("Duplicate registration was accepted."); }
        catch (ConflictException) { }
    }
    var registered = await db.Tenants.SingleAsync(t => t.Slug == slug);
    registered.IsDeleted = true;
    await db.SaveChangesAsync();
    try { await handler.Handle(new("Other store", "deleted" + suffix, "Owner", email), default); throw new Exception("Deleted tenant email was reused."); }
    catch (ConflictException) { }

    // Bypass the application checks to verify the database enforces concurrent-write safety.
    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS ix_tenants_owner_email ON tenant.tenants(owner_email);");
    var duplicateTenant = Tenant.Create("Race store", "race" + suffix, ownerEmail: email);
    db.Tenants.Add(duplicateTenant);
    try { await db.SaveChangesAsync(); throw new Exception("Database accepted a duplicate email."); }
    catch (ConflictException) { db.Entry(duplicateTenant).State = EntityState.Detached; }

    if (sender.Messages.Count != 2) throw new Exception("Duplicate registrations sent emails.");
    Console.WriteLine("PASS: duplicate slug/email checks, deleted tenant reservation and database email uniqueness. All changes rolled back.");
}
finally { await transaction.RollbackAsync(); }

sealed class CapturingEmailSender : IEmailSender
{
    public List<(string Subject, string Body)> Messages { get; } = [];
    public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken) { Messages.Add((subject, htmlBody)); return Task.CompletedTask; }
    public Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken) => Task.FromResult(false);
}

sealed class TestTemplateStore : IRegistrationEmailTemplateStore
{
    public Task<RegistrationEmailTemplate> GetAsync(CancellationToken ct) => Task.FromResult(RegistrationEmailTemplate.Default);
    public Task<RegistrationEmailTemplate> GetAsync(string key, CancellationToken ct) => Task.FromResult(RegistrationEmailTemplate.GetDefault(key));
    public Task SaveAsync(RegistrationEmailTemplate template, CancellationToken ct) => Task.CompletedTask;
    public Task SaveAsync(string key, RegistrationEmailTemplate template, CancellationToken ct) => Task.CompletedTask;
    public Task ResetAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ResetAsync(string key, CancellationToken ct) => Task.CompletedTask;
}
