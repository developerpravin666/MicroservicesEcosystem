using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Users.Commands.Login;
using AccountingInventory.Application.Users.Commands.Refresh;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using AccountingInventory.Infrastructure.Security;
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

// All writes are confined to a unique scratch database, never existing tenants.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,
    "src/AccountingInventory/AccountingInventory.Api/appsettings.json")));
var connectionSettings = new NpgsqlConnectionStringBuilder(settings.RootElement
    .GetProperty("ConnectionStrings").GetProperty("AccountingInventoryDb").GetString());
if (!args.Contains("--local") || connectionSettings.Host is not ("localhost" or "127.0.0.1" or "::1"))
    throw new InvalidOperationException("Use --local with local PostgreSQL settings.");
var scratch = "login_check_" + Guid.NewGuid().ToString("N");
connectionSettings.Database = "postgres";
await using var admin = new NpgsqlConnection(connectionSettings.ConnectionString);
await admin.OpenAsync();
await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{scratch}\"", admin))
    await command.ExecuteNonQueryAsync();
try
{
    connectionSettings.Database = scratch;
    connectionSettings.Pooling = false;
    var connectionString = connectionSettings.ConnectionString;
    var directoryOptions = new DbContextOptionsBuilder<TenantDbContext>()
        .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;
    await using var directory = new TenantDbContext(directoryOptions);
    await directory.Database.EnsureCreatedAsync();
    var first = Tenant.Create("First Store", "first"); first.Activate();
    var second = Tenant.Create("Second Store", "second"); second.Activate();
    var suspended = Tenant.Create("Suspended Store", "suspended"); suspended.Activate(); suspended.Suspend();
    directory.Tenants.AddRange(first, second, suspended);
    await directory.SaveChangesAsync();
    var hasher = new PasswordHasher<User>();
    var firstUser = User.Create("sharedname", "9000000001", "first@example.com");
    var secondUser = User.Create("sharedname", "9000000002", "second@example.com");
    firstUser.SetOwner(true); firstUser.AcceptInvitation();
    secondUser.SetOwner(true); secondUser.AcceptInvitation();
    var staff = User.Create("staff", "9000000003", "staff@example.com");
    var inactive = User.Create("inactive", "9000000004", "inactive@example.com"); inactive.DeActivate();
    var deleted = User.Create("deleted", "9000000005", "deleted@example.com"); deleted.Delete();
    var suspendedUser = User.Create("owner", "9000000006", "suspended@example.com");
    var context = new TestTenantContext();
    var provider = new DynamicTenantProvider(context);
    var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
        .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;
    foreach (var (tenant, users) in new[] {
        (first, new[] {firstUser, staff, inactive, deleted}),
        (second, new[] {secondUser}), (suspended, new[] {suspendedUser}) })
    {
        context.SetTenant(tenant.Id, tenant.SchemaName);
        await using var seed = new AccountingInventoryDbContext(options, provider);
        await seed.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{tenant.SchemaName}\"");
        await seed.ResetConnectionAsync(default);
        await seed.Database.ExecuteSqlRawAsync(seed.Database.GenerateCreateScript());
        foreach (var user in users) user.SetPasswordHash(hasher.HashPassword(user, "Test@123456"));
        seed.Users.AddRange(users);
        await seed.SaveChangesAsync();
    }
    var tokens = new TokenService(new JwtOptions {
        Issuer = "login-check", Audience = "login-check",
        SigningKey = "login-check-only-signing-key-at-least-32-characters" });
    async Task<LoginResult> Login(string? slug, string identifier, string password = "Test@123456")
    {
        context.Clear();
        await using var db = new AccountingInventoryDbContext(options, provider);
        return await new LoginCommandHandler(db, directory, tokens, context, hasher,
            NullLogger<LoginCommandHandler>.Instance).Handle(new(slug, identifier, password), default);
    }
    async Task Reject(string? slug, string identifier, string password = "Test@123456")
    {
        try { await Login(slug, identifier, password); throw new Exception("Invalid login accepted: " + identifier); }
        catch (UnauthorizedException) { }
    }
    var validator = new LoginCommandValidator();
    Check(validator.Validate(new LoginCommand(null, "first@example.com", "password")).IsValid, "Omitted slug validates");
    Check(validator.Validate(new LoginCommand("first", "sharedname", "password")).IsValid, "Legacy request validates");
    Check(!validator.Validate(new LoginCommand(null, "", "")).IsValid, "Empty credentials rejected");
    foreach (var (slug, identifier, expected, expectedUser) in new[] {
        ((string?)null, "  FIRST@EXAMPLE.COM  ", first, firstUser),
        (null, "9000000002", second, secondUser),
        ("", "staff@example.com", first, staff),
        (" FIRST ", "sharedname", first, firstUser),
        ("second", "sharedname", second, secondUser) })
    {
        var result = await Login(slug, identifier);
        Check(result.TenantId == expected.Id && result.UserId == expectedUser.Id &&
            result.TenantSlug == expected.Slug && result.UserName == expectedUser.UserName, "Correct tenant and account: " + identifier);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Check(jwt.Claims.Single(c => c.Type == "tenant_id").Value == expected.Id.ToString() &&
            jwt.Claims.Single(c => c.Type == "tenant_schema").Value == expected.SchemaName, "JWT isolation");
        context.SetTenant(expected.Id, expected.SchemaName);
        await using var verify = new AccountingInventoryDbContext(options, provider);
        await verify.ResetConnectionAsync(default);
        var saved = await verify.Users.SingleAsync(u => u.Id == expectedUser.Id);
        Check(saved.IsRefreshTokenValid(tokens.HashRefreshToken(result.RefreshToken), DateTimeOffset.UtcNow), "Refresh persisted in selected schema");
        context.Clear();
        await using var refreshDb = new AccountingInventoryDbContext(options, provider);
        var refreshed = await new RefreshTokenCommandHandler(refreshDb, tokens, directory, context,
            NullLogger<RefreshTokenCommandHandler>.Instance).Handle(new(result.AccessToken, result.RefreshToken), default);
        var refreshedJwt = new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken);
        Check(refreshedJwt.Claims.Single(c => c.Type == "tenant_id").Value == expected.Id.ToString(), "Refresh retains selected tenant without slug");
        Check(refreshed.RefreshToken != result.RefreshToken, "Refresh token rotates");
    }
    await Reject(null, "first@example.com", "wrong");
    await Reject(null, "sharedname");
    await Reject(null, "missing@example.com");
    await Reject(null, "inactive@example.com");
    await Reject(null, "deleted@example.com");
    await Reject(null, "suspended@example.com");
    await Reject("suspended", "owner");
    await Reject("missing", "sharedname");
    // Same email/mobile across schemas must never silently select a store.
    context.SetTenant(second.Id, second.SchemaName);
    await using (var duplicateDb = new AccountingInventoryDbContext(options, provider))
    {
        await duplicateDb.ResetConnectionAsync(default);
        var duplicate = User.Create("duplicate", firstUser.Mobile, firstUser.Email);
        duplicate.SetPasswordHash(hasher.HashPassword(duplicate, "Different@123456"));
        duplicateDb.Users.Add(duplicate); await duplicateDb.SaveChangesAsync();
    }
    await Reject(null, firstUser.Email); await Reject(null, firstUser.Mobile);
    Check((await Login("first", "sharedname")).TenantId == first.Id, "Slug resolves duplicate identity");
    context.SetTenant(first.Id, first.SchemaName);
    await using (var duplicateDb = new AccountingInventoryDbContext(options, provider))
    {
        await duplicateDb.ResetConnectionAsync(default);
        var duplicate = User.Create("same-mobile", staff.Mobile, "same-mobile@example.com");
        duplicate.SetPasswordHash(hasher.HashPassword(duplicate, "Test@123456"));
        duplicateDb.Users.Add(duplicate); await duplicateDb.SaveChangesAsync();
    }
    await Reject(null, staff.Mobile);
    var administration = new TenantAdministration(directory, hasher, new TestActor());
    var profile = new UpdateTenantProfileRequest("Updated Store", "Updated Owner", "updated@example.com",
        "9000000010", "23", "23ABCDE1234F1Z5", "Updated address");
    await administration.UpdateProfileAsync(first.Id, profile, default);
    var updated = await directory.Tenants.AsNoTracking().SingleAsync(t => t.Id == first.Id);
    Check(updated.Name == profile.Name && updated.OwnerEmail == profile.OwnerEmail && updated.Slug == first.Slug &&
        updated.SchemaName == first.SchemaName, "Profile updated without changing tenant routing");
    var beforeReset = await Login(null, "updated@example.com");
    Check(beforeReset.UserName == firstUser.UserName, "Contact change preserves username and email login");
    Check((await Login(null, "9000000010")).TenantId == first.Id, "Updated mobile login");
    try { await administration.UpdateProfileAsync(second.Id, profile, default); throw new Exception("Duplicate tenant email accepted."); }
    catch (ConflictException) { }
    try { await administration.UpdateProfileAsync(first.Id, profile with { Name = "Must rollback", OwnerEmail = staff.Email }, default); throw new Exception("Duplicate staff email accepted."); }
    catch (ConflictException) { }
    Check((await directory.Tenants.AsNoTracking().SingleAsync(t => t.Id == first.Id)).Name == "Updated Store", "Conflicting profile does not partially save");
    await administration.ResetOwnerPasswordAsync(first.Id, "Changed@123456", default);
    await Reject(null, "updated@example.com");
    context.SetTenant(first.Id, first.SchemaName);
    await using (var passwordDb = new AccountingInventoryDbContext(options, provider))
    {
        await passwordDb.ResetConnectionAsync(default);
        Check((await passwordDb.Users.SingleAsync(u => u.Id == firstUser.Id)).RefreshTokenHash is null, "Admin password reset revokes refresh token");
    }
    Check((await Login(null, "updated@example.com", "Changed@123456")).UserId == firstUser.Id, "Reset password logs into same account");
    await Reject("second", "sharedname", "Changed@123456");
    var pending = Tenant.Create("Pending Store", "pending", ownerName: "Pending", ownerEmail: "pending@example.com", ownerMobile: "9000000011");
    directory.Tenants.Add(pending); await directory.SaveChangesAsync();
    await administration.UpdateProfileAsync(pending.Id, profile with { Name = "Pending Updated", OwnerEmail = "pending-new@example.com", OwnerMobile = "9000000011" }, default);
    await administration.ResetOwnerPasswordAsync(pending.Id, "Pending@123456", default);
    var pendingSaved = await directory.Tenants.AsNoTracking().SingleAsync(t => t.Id == pending.Id);
    Check(pendingSaved.Status == TenantStatus.PendingApproval && hasher.VerifyHashedPassword(firstUser,
        pendingSaved.InitialPasswordHash!, "Pending@123456") != PasswordVerificationResult.Failed, "Pending profile/password updated without provisioning or activation");
    Check((await Login("system", "admin", "Admin@123456")).TenantId == Guid.Empty, "Legacy Product Owner flow");
    await TenantManagementApiChecks.RunAsync();
    Console.WriteLine("PASS: email/mobile/staff discovery, legacy login, validation, JWT/schema isolation, refresh persistence, invalid/inactive/deleted/suspended accounts and ambiguity.");
}
finally
{
    await using var drop = new NpgsqlCommand($"DROP DATABASE \"{scratch}\" WITH (FORCE)", admin);
    await drop.ExecuteNonQueryAsync();
}
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}
sealed class TestTenantContext : ITenantContext, ITenantContextAccessor
{
    public Guid? TenantId { get; private set; }
    public string? SchemaName { get; private set; }
    public void SetTenant(Guid tenantId, string schemaName) { TenantId = tenantId; SchemaName = schemaName; }
    public void Clear() { TenantId = null; SchemaName = null; }
}
sealed class TestActor : BuildingBlocks.Persistence.ICurrentUserProvider
{ public Guid? UserId => Guid.Parse("00000000-0000-0000-0000-000000000001"); }
