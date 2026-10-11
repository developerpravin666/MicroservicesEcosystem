using BuildingBlocks.Domain;
using BuildingBlocks.Persistence;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace AccountingInventory.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that stamps CreatedAt/CreatedBy on insert and
/// ModifiedAt/ModifiedBy on update for every AuditableEntity — automatically,
/// with zero code required in Application-layer handlers. Deletes are converted to
/// soft-deletes so audit history is never lost.
/// </summary>
public sealed class AuditableEntitySaveChangesInterceptor(
    ICurrentUserProvider currentUserProvider,
    ITenantProvider tenantProvider)
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

        // Be explicit here: the interceptor adds AuditLog entities while SaveChanges
        // is in progress, and we need to snapshot the user's pending entity changes
        // before adding those records to the same unit of work.
        context.ChangeTracker.DetectChanges();

        var actor = currentUserProvider.UserId;
        var now = DateTimeOffset.UtcNow;

        var entries = context.ChangeTracker.Entries<AuditableEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var auditLogs = new List<AuditLog>();

        foreach (var entry in entries)
        {
            var stateBeforeAudit = entry.State;
            var tableName = entry.Metadata.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName)) continue;
            var recordId = entry.Property(nameof(AuditableEntity.Id)).CurrentValue?.ToString() ?? string.Empty;
            var oldValues = stateBeforeAudit is EntityState.Modified or EntityState.Deleted
                ? Snapshot(entry, originalValues: true, includeAll: stateBeforeAudit == EntityState.Deleted)
                : null;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;
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

            var action = stateBeforeAudit switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ when entry.Property(nameof(AuditableEntity.IsDeleted)).CurrentValue is true
                    && entry.Property(nameof(AuditableEntity.IsDeleted)).OriginalValue is false => "Delete",
                _ => "Update"
            };
            var newValues = Snapshot(entry, originalValues: false, includeAll: stateBeforeAudit is EntityState.Added or EntityState.Deleted);
            auditLogs.Add(AuditLog.Create(tableName, recordId, action,
                oldValues, newValues, actor, now, tenantProvider.SchemaName));
        }

        if (auditLogs.Count > 0) context.Set<AuditLog>().AddRange(auditLogs);
    }

    private static string? Snapshot(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        bool originalValues, bool includeAll)
    {
        var values = entry.Properties
            .Where(property => includeAll || property.IsModified)
            .Where(property => !IsSensitive(property.Metadata.Name))
            .ToDictionary(property => property.Metadata.Name,
                property => originalValues ? property.OriginalValue : property.CurrentValue);
        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static bool IsSensitive(string propertyName)
        => propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase);
}
