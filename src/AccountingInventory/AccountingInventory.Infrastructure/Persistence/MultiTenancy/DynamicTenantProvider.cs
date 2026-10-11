using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;

namespace AccountingInventory.Infrastructure.Persistence.MultiTenancy;

/// <summary>
/// Resolves the tenant schema established for the current operation. HTTP callers
/// are resolved from their tenant id by the API endpoint filter; they never supply a
/// database schema themselves. Background work can establish the same context
/// directly through <see cref="ITenantContextAccessor"/>.
/// </summary>
public sealed class DynamicTenantProvider(ITenantContext tenantContext) : ITenantProvider
{
    public const string DefaultSchema = "public";

    public string SchemaName
    {
        get
        {
            // Supports both HTTP flows, after their tenant id has been resolved
            // against the control-plane registry, and non-HTTP provisioning flows.
            return tenantContext.SchemaName is { Length: > 0 } contextSchema
                ? TenantSchemaNameValidator.EnsureValid(contextSchema)
                : DefaultSchema;
        }
    }
}
