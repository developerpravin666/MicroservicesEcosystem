namespace AccountingInventory.Infrastructure.Persistence.MultiTenancy;

/// <summary>Provides the PostgreSQL schema for the current unit of work.</summary>
public interface ITenantProvider
{
    string SchemaName { get; }
}
