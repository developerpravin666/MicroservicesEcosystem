namespace AccountingInventory.Application.Abstractions;

public interface IDatabaseConnectionManager
{
    string ActiveTarget { get; }
    string GetActiveConnectionString();
    string GetConnectionString(string target);
    DatabaseConfigSummary GetConfiguration();
    Task<DatabaseConnectionTestResult> TestConnectionAsync(string? connectionString = null, string? target = null, CancellationToken ct = default);
    Task<DatabaseSwitchResult> SwitchTargetAsync(string target, string? customConnectionString, IServiceProvider serviceProvider, bool syncTenants = false, CancellationToken ct = default);
    void UpdateCloudConnectionString(string connectionString);
    void UpdateLocalConnectionString(string connectionString);
}

public sealed record DatabaseConfigSummary(
    string ActiveTarget,
    DatabaseTargetDetails Local,
    DatabaseTargetDetails Cloud);

public sealed record DatabaseTargetDetails(
    string Target,
    string Host,
    int Port,
    string Database,
    string Username,
    bool IsConfigured,
    string MaskedConnectionString,
    string RawConnectionString);

public sealed record DatabaseConnectionTestResult(
    bool Success,
    string Target,
    long LatencyMs,
    string? ServerVersion,
    string Message);

public sealed record DatabaseSwitchResult(
    bool Success,
    string ActiveTarget,
    string Message,
    int SchemasMigrated,
    DateTimeOffset Timestamp);

