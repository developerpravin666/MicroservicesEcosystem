using System.Data;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Infrastructure.Persistence;

// Registry and owner changes share one physical connection and transaction.
public sealed class TenantAdministration(TenantDbContext registry, IPasswordHasher<User> hasher,
    BuildingBlocks.Persistence.ICurrentUserProvider currentUser) : ITenantAdministration
{
    public Task UpdateProfileAsync(Guid tenantId, UpdateTenantProfileRequest request, CancellationToken ct) =>
        ChangeAsync(tenantId, request, null, ct);

    public Task ResetOwnerPasswordAsync(Guid tenantId, string password, CancellationToken ct) =>
        ChangeAsync(tenantId, null, password, ct);

    private async Task ChangeAsync(Guid tenantId, UpdateTenantProfileRequest? profile, string? password, CancellationToken ct)
    {
        var target = await registry.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("Tenant was not found.");
        var connectionString = TenantSchemaMigrator.CreateTenantConnectionString(
            registry.Database.GetConnectionString()!, target.SchemaName);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var directoryOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options;
        await using var directory = new TenantDbContext(directoryOptions);
        await directory.Database.UseTransactionAsync(transaction, ct);
        var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("Tenant was not found.");
        var provisioned = tenant.Status is TenantStatus.Active or TenantStatus.Suspended;
        AccountingInventoryDbContext? db = null;
        try
        {
            User? owner = null;
            if (provisioned)
            {
                var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
                    .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options;
                db = new AccountingInventoryDbContext(options, new ProfileTenantProvider(tenant.SchemaName), currentUser);
                await db.Database.UseTransactionAsync(transaction, ct);
                var owners = await db.Users.Where(u => u.IsOwner).Take(2).ToListAsync(ct);
                if (owners.Count != 1) throw new ConflictException("The store must have exactly one owner before its login details can be changed.");
                owner = owners[0];
            }
            if (profile is not null)
            {
                var email = profile.OwnerEmail.Trim().ToLowerInvariant();
                var mobile = profile.OwnerMobile.Trim();
                if (await directory.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id != tenantId &&
                    t.OwnerEmail != null && t.OwnerEmail.Trim().ToLower() == email, ct))
                    throw new ConflictException("A tenant with this email already exists.");
                if (owner is not null)
                {
                    if (await db!.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != owner.Id &&
                        (u.Email.Trim().ToLower() == email || u.Mobile.Trim() == mobile), ct))
                        throw new ConflictException("Another account in this store already uses this email or mobile.");
                    if (owner.Email != email || owner.Mobile != mobile) owner.UpdateContact(email, mobile);
                }
                tenant.UpdateProfile(profile.Name, profile.OwnerName, email, mobile, profile.StateCode, profile.Gstin, profile.Address);
            }
            if (password is not null)
            {
                if (provisioned)
                {
                    if (owner is not { IsActive: true, EmailVerified: true })
                        throw new ConflictException("The owner must activate their account before an administrator can reset their password.");
                    owner.SetPasswordHash(hasher.HashPassword(owner, password));
                    owner.RevokeRefreshToken();
                }
                else if (tenant.Status is not (TenantStatus.PendingApproval or TenantStatus.PendingProvisioning))
                    throw new ConflictException("Passwords can only be reset for pending, active or suspended stores.");
                // Used when a pending registration is approved; never returned to callers.
                var passwordOwner = owner ?? User.Create("owner", tenant.OwnerMobile ?? "0000000000", tenant.OwnerEmail ?? "owner@store.local");
                tenant.SetInitialPasswordHash(hasher.HashPassword(passwordOwner, password));
            }
            tenant.ModifiedAt = DateTimeOffset.UtcNow;
            tenant.ModifiedBy = currentUser.UserId;
            if (db is not null) await db.SaveChangesAsync(ct);
            await directory.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (FindPostgres(ex)?.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            throw new ConflictException("The details conflict with another account or changed concurrently. Refresh and retry; no changes were saved.");
        }
        finally { if (db is not null) await db.DisposeAsync(); }
    }

    private static PostgresException? FindPostgres(Exception ex) => ex is PostgresException pg
        ? pg : ex.InnerException is { } inner ? FindPostgres(inner) : null;

    private sealed class ProfileTenantProvider(string schema) : MultiTenancy.ITenantProvider
    { public string SchemaName => schema; }
}
