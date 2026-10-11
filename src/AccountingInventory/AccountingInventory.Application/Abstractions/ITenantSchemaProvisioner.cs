namespace AccountingInventory.Application.Abstractions;

/// <summary>
/// Dependency-inversion seam over BuildingBlocks.Persistence's TenantSchemaProvisioner
/// (an EF Core-specific type that Application-layer code must not reference directly).
/// Implemented in Infrastructure as a thin adapter around the shared generic
/// provisioner, bound to IdentityDbContext.
/// </summary>
public interface ITenantSchemaProvisioner
{
    Task ProvisionAsync(Guid tenantId, string schemaName, CancellationToken cancellationToken);
}
