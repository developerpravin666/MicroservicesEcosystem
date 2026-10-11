using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Persistence;

/// <summary>
/// EF Core SaveChanges interceptor shared by every service's DbContext. Stamps
/// CreatedAtUtc/CreatedBy on insert and LastModifiedAtUtc/LastModifiedBy on update for
/// every AuditableEntity, converts deletes into soft-deletes so audit history is never
/// lost, and — for any entity implementing ITenantEntity — stamps TenantId from the
/// current request's ITenantContext on insert, so application code never has to set
/// it explicitly (and can't forget to).
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor(
    ICurrentUserProvider currentUserProvider, ITenantContext tenantContext)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context is null) return;

        var actor = currentUserProvider.UserId;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;

                    if (entry.Entity is ITenantEntity tenantEntity && tenantContext.TenantId is { } tenantId)
                    {
                        tenantEntity.TenantId = tenantId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.ModifiedAt).CurrentValue = now;
                    entry.Property(e => e.ModifiedBy).CurrentValue = actor;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Property(e => e.IsDeleted).CurrentValue = true;
                    entry.Property(e => e.ModifiedAt).CurrentValue = now;
                    entry.Property(e => e.ModifiedBy).CurrentValue = actor;
                    break;
            }
        }
    }
}
