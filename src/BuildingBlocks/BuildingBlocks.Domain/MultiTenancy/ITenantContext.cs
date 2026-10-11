namespace BuildingBlocks.Domain.MultiTenancy;

/// <summary>
/// Read-only view of "which tenant, and which PostgreSQL schema, is the current
/// request/operation for". Framework-agnostic on purpose — it lives in this
/// dependency-free shared kernel rather than in BuildingBlocks.Security (which pulls
/// in ASP.NET Core's JwtBearer package) specifically so Application-layer code in
/// every service (Identity, Inventory, Tenant, ...) can depend on this abstraction
/// without violating Clean Architecture's "Application must not depend on framework
/// packages" rule. BuildingBlocks.Security provides the concrete implementation
/// (populated from JWT claims) that's wired up at the host/Infrastructure level.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    string? SchemaName { get; }
}

/// <summary>
/// Write side of the same instance as ITenantContext. Needed by:
///   1. Middleware that resolves tenant from JWT claims (BuildingBlocks.Security).
///   2. Application-layer handlers that resolve tenant themselves because there's no
///      JWT yet (Login/Register) or because they're reacting to an event that names
///      the tenant directly (TenantProvisioningConsumer).
/// </summary>
public interface ITenantContextAccessor
{
    void SetTenant(Guid tenantId, string schemaName);
    void Clear();
}
