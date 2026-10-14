namespace AccountingInventory.Application.Abstractions;

/// <summary>
/// Centralized coordinator for transactional, resilient database migrations across
/// the control plane schema ('tenant') and isolated multi-tenant schemas.
/// </summary>
public interface IDatabaseMigrationCoordinator
{
    /// <summary>
    /// Executes the full platform migration: migrates the control plane schema inside a transaction,
    /// then sequentially migrates all active tenant schemas inside isolated transactions.
    /// </summary>
    Task<MigrationExecutionResult> MigrateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates only the master/control-plane registry schema ('tenant') within an explicit transaction.
    /// </summary>
    Task<MigrationExecutionResult> MigrateControlPlaneAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates a specific tenant's schema within an isolated transaction.
    /// </summary>
    Task<MigrationExecutionResult> MigrateTenantSchemaAsync(string schemaName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates all active tenant schemas within per-schema isolated transactions.
    /// </summary>
    Task<MigrationExecutionResult> MigrateAllTenantSchemasAsync(CancellationToken cancellationToken = default);

}

public sealed record MigrationExecutionResult(
    bool Success,
    string Message,
    int TotalSchemas,
    int SuccessfulSchemas,
    IReadOnlyList<string> MigratedSchemas,
    IReadOnlyDictionary<string, string> FailedSchemas,
    DateTimeOffset Timestamp);

