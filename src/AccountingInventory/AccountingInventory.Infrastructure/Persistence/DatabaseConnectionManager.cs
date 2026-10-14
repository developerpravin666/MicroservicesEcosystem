using AccountingInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AccountingInventory.Infrastructure.Persistence;

public sealed class DatabaseConnectionManager : IDatabaseConnectionManager
{
    private readonly object _lock = new();
    private readonly ILogger<DatabaseConnectionManager> _logger;

    private string _activeTarget = "Local";
    private string _localConnectionString = "";
    private string _cloudConnectionString = "";

    public string ActiveTarget
    {
        get
        {
            lock (_lock)
            {
                return _activeTarget;
            }
        }
    }

    public DatabaseConnectionManager(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<DatabaseConnectionManager> logger)
    {
        _logger = logger;
        // ASP.NET Core loads appsettings.json, then appsettings.{Environment}.json,
        // followed by environment overrides. Never load persisted local database state.
        _activeTarget = hostEnvironment.IsProduction() ? "Cloud" : "Local";
        var connectionString = configuration.GetConnectionString("AccountingInventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");
        }

        _localConnectionString = configuration["DatabaseSettings:LocalConnectionString"] ?? "";
        _cloudConnectionString = configuration["DatabaseSettings:CloudConnectionString"] ?? "";
        if (_activeTarget == "Cloud")
        {
            _cloudConnectionString = connectionString;
        }
        else
        {
            _localConnectionString = connectionString;
        }

        _logger.LogInformation("Initialized database configuration from application settings. Environment: {Environment}. Active target: {Target}",
            hostEnvironment.EnvironmentName, _activeTarget);
    }
    public string GetActiveConnectionString()
    {
        lock (_lock)
        {
            if (string.Equals(_activeTarget, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return _cloudConnectionString;
            }

            return _localConnectionString;
        }
    }

    public string GetConnectionString(string target)
    {
        lock (_lock)
        {
            if (string.Equals(target, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return _cloudConnectionString;
            }

            return _localConnectionString;
        }
    }

    public DatabaseConfigSummary GetConfiguration()
    {
        lock (_lock)
        {
            var localDetails = ParseTargetDetails("Local", _localConnectionString);
            var cloudDetails = ParseTargetDetails("Cloud", _cloudConnectionString);

            return new DatabaseConfigSummary(
                ActiveTarget: _activeTarget,
                Local: localDetails,
                Cloud: cloudDetails);
        }
    }

    public void UpdateCloudConnectionString(string connectionString)
    {
        lock (_lock)
        {
            _cloudConnectionString = connectionString?.Trim() ?? "";
        }
    }

    public void UpdateLocalConnectionString(string connectionString)
    {
        lock (_lock)
        {
            _localConnectionString = connectionString?.Trim() ?? "";
        }
    }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        string? connectionString = null,
        string? target = null,
        CancellationToken ct = default)
    {
        string targetConnStr;
        string resolvedTarget;

        lock (_lock)
        {
            resolvedTarget = target ?? _activeTarget;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                targetConnStr = connectionString.Trim();
            }
            else if (string.Equals(resolvedTarget, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                targetConnStr = _cloudConnectionString;
            }
            else
            {
                targetConnStr = _localConnectionString;
            }
        }

        if (string.IsNullOrWhiteSpace(targetConnStr))
        {
            return new DatabaseConnectionTestResult(
                Success: false,
                Target: resolvedTarget,
                LatencyMs: 0,
                ServerVersion: null,
                Message: $"{resolvedTarget} connection string is not configured.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var conn = new NpgsqlConnection(targetConnStr);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand("SELECT version();", conn);
            var versionObj = await cmd.ExecuteScalarAsync(ct);
            sw.Stop();

            var version = versionObj?.ToString() ?? "PostgreSQL";
            return new DatabaseConnectionTestResult(
                Success: true,
                Target: resolvedTarget,
                LatencyMs: sw.ElapsedMilliseconds,
                ServerVersion: version,
                Message: $"Successfully connected in {sw.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Failed database connection probe to {Target}", resolvedTarget);
            return new DatabaseConnectionTestResult(
                Success: false,
                Target: resolvedTarget,
                LatencyMs: sw.ElapsedMilliseconds,
                ServerVersion: null,
                Message: $"Connection failed: {ex.Message}");
        }
    }

    public async Task<DatabaseSwitchResult> SwitchTargetAsync(
        string target,
        string? customConnectionString,
        IServiceProvider serviceProvider,
        bool syncTenants = false,
        CancellationToken ct = default)
    {
        var normalizedTarget = string.Equals(target, "Cloud", StringComparison.OrdinalIgnoreCase) ? "Cloud" : "Local";

        string targetConnStr;
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(customConnectionString))
            {
                if (normalizedTarget == "Cloud")
                {
                    _cloudConnectionString = customConnectionString.Trim();
                }
                else
                {
                    _localConnectionString = customConnectionString.Trim();
                }
            }

            targetConnStr = normalizedTarget == "Cloud" ? _cloudConnectionString : _localConnectionString;
        }

        if (string.IsNullOrWhiteSpace(targetConnStr))
        {
            throw new InvalidOperationException($"Cannot switch to {normalizedTarget}: connection string is empty.");
        }

        // 1. Verify reachability
        var test = await TestConnectionAsync(targetConnStr, normalizedTarget, ct);
        if (!test.Success)
        {
            throw new InvalidOperationException($"Cannot switch to {normalizedTarget} database: {test.Message}");
        }

        // 2. Prepare DbContextOptionsBuilder for the target database
        var targetOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(targetConnStr, npgsql => npgsql.MigrationsHistoryTable("__ControlPlaneHistory", "tenant"))
            .UseSnakeCaseNamingConvention()
            .Options;

        int migratedSchemaCount = 0;

        await using (var targetTenantDb = new TenantDbContext(targetOptions))
        {
            // 3. Migrate control plane on target database with transactional safety
            await targetTenantDb.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS \"tenant\";", ct);
            await targetTenantDb.Database.MigrateAsync(ct);

            await using (var tx = await targetTenantDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct))
            {
                try
                {
                    await targetTenantDb.Database.ExecuteSqlRawAsync(DatabaseMigrationCoordinator.ControlPlaneSupplementalSql, ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }

            // 4. Optionally synchronize tenant metadata from current database
            if (syncTenants)
            {
                string currentConnStr;
                lock (_lock)
                {
                    currentConnStr = GetActiveConnectionString();
                }

                if (!string.Equals(currentConnStr, targetConnStr, StringComparison.Ordinal))
                {
                    try
                    {
                        var currentOptions = new DbContextOptionsBuilder<TenantDbContext>()
                            .UseNpgsql(currentConnStr, npgsql => npgsql.MigrationsHistoryTable("__ControlPlaneHistory", "tenant"))
                            .UseSnakeCaseNamingConvention()
                            .Options;

                        await using var currentTenantDb = new TenantDbContext(currentOptions);
                        var sourceTenants = await currentTenantDb.Tenants.AsNoTracking().ToListAsync(ct);
                        var existingTargetIds = await targetTenantDb.Tenants.Select(t => t.Id).ToHashSetAsync(ct);

                        foreach (var t in sourceTenants)
                        {
                            if (!existingTargetIds.Contains(t.Id))
                            {
                                await targetTenantDb.Database.ExecuteSqlRawAsync(@"
                                    INSERT INTO tenant.tenants (
                                        id, name, slug, schema_name, status, is_active, is_deleted, created_at,
                                        owner_name, owner_email, owner_mobile, initial_password_hash, state_code, gstin, address, rejection_reason
                                    ) VALUES (
                                        {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}
                                    ) ON CONFLICT (id) DO NOTHING;
                                ", t.Id, t.Name, t.Slug, t.SchemaName, (int)t.Status, t.IsActive, t.IsDeleted, t.CreatedAt,
                                   t.OwnerName, t.OwnerEmail, t.OwnerMobile, t.InitialPasswordHash, t.StateCode, t.Gstin, t.Address, t.RejectionReason);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not synchronize existing tenants from previous database to target database.");
                    }
                }
            }

            // 5. Run tenant schema migrations on target database
            var activeSchemas = await targetTenantDb.Tenants
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.IsActive)
                .Select(t => t.SchemaName)
                .ToListAsync(ct);

            foreach (var schemaName in activeSchemas.Distinct(StringComparer.Ordinal))
            {
                await TenantSchemaMigrator.MigrateAsync(targetConnStr, schemaName, ct);
                migratedSchemaCount++;
            }
        }

        // 6. Apply the target switch for this running process
        lock (_lock)
        {
            _activeTarget = normalizedTarget;
        }

        _logger.LogInformation("Database switch to {Target} successful. {Count} schemas migrated.", normalizedTarget, migratedSchemaCount);

        return new DatabaseSwitchResult(
            Success: true,
            ActiveTarget: normalizedTarget,
            Message: $"Successfully pointed application to {normalizedTarget} PostgreSQL database. {migratedSchemaCount} active tenant schemas verified.",
            SchemasMigrated: migratedSchemaCount,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private static DatabaseTargetDetails ParseTargetDetails(string target, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseTargetDetails(
                Target: target,
                Host: "",
                Port: 0,
                Database: "",
                Username: "",
                IsConfigured: false,
                MaskedConnectionString: "",
                RawConnectionString: "");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var maskedBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(maskedBuilder.Password))
            {
                maskedBuilder.Password = "********";
            }

            return new DatabaseTargetDetails(
                Target: target,
                Host: string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host,
                Port: builder.Port > 0 ? builder.Port : 5432,
                Database: string.IsNullOrWhiteSpace(builder.Database) ? "" : builder.Database,
                Username: string.IsNullOrWhiteSpace(builder.Username) ? "" : builder.Username,
                IsConfigured: true,
                MaskedConnectionString: maskedBuilder.ConnectionString,
                RawConnectionString: connectionString);
        }
        catch
        {
            return new DatabaseTargetDetails(
                Target: target,
                Host: "Invalid format",
                Port: 0,
                Database: "",
                Username: "",
                IsConfigured: false,
                MaskedConnectionString: "Invalid connection string",
                RawConnectionString: connectionString);
        }
    }

}
