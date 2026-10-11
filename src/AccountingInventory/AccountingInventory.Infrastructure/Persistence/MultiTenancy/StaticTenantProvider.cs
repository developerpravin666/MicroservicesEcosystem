using BuildingBlocks.Domain;

namespace AccountingInventory.Infrastructure.Persistence.MultiTenancy;

/// <summary>Schema provider used only by startup migration work.</summary>
internal sealed class StaticTenantProvider(string schemaName) : ITenantProvider
{
    public string SchemaName { get; } = TenantSchemaNameValidator.EnsureValid(schemaName);
}
