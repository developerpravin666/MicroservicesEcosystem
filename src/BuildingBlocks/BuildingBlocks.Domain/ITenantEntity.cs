namespace BuildingBlocks.Domain;

/// <summary>
/// Implemented by any entity that belongs to exactly one tenant. Any DbContext built
/// on top of BuildingBlocks.Persistence gets two things for free from this marker:
///   1. TenantId is stamped automatically on insert (from the current request's
///      ITenantContext) by the shared AuditableEntitySaveChangesInterceptor.
///   2. A global EF Core query filter restricting every query to the current tenant
///      can be applied generically in OnModelCreating (see BuildingBlocks.Persistence's
///      ModelBuilderExtensions.ApplyTenantQueryFilters).
/// Entities that are NOT tenant-scoped (e.g. TenantService's own Tenant aggregate —
/// it IS the tenant, not owned by one) simply don't implement this interface.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
