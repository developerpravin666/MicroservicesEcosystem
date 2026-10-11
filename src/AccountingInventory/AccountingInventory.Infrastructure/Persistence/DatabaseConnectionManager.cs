using System.Text.Json;
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
    private readonly string _configFilePath;
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
        _configFilePath = Path.Combine(hostEnvironment.ContentRootPath, "database.config.json");

        LoadInitialConfiguration(configuration);
    }

    private void LoadInitialConfiguration(IConfiguration configuration)
    {
        lock (_lock)
        {
            var loadedFromFile = false;
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var content = File.ReadAllText(_configFilePath);
                    var stored = JsonSerializer.Deserialize<StoredDatabaseConfig>(content);
                    if (stored != null)
                    {
                        _activeTarget = string.Equals(stored.ActiveTarget, "Cloud", StringComparison.OrdinalIgnoreCase) ? "Cloud" : "Local";
                        _localConnectionString = stored.LocalConnectionString ?? "";
                        _cloudConnectionString = stored.CloudConnectionString ?? "";
                        loadedFromFile = true;
                        _logger.LogInformation("Loaded database configuration from {Path}. Active target: {Target}", _configFilePath, _activeTarget);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read database configuration from {Path}. Falling back to appsettings.", _configFilePath);
                }
            }

            if (!loadedFromFile)
            {
                var defaultConn = configuration.GetConnectionString("AccountingInventoryDb") ?? "Host=localhost;Port=5432;Database=accounting_inventory;Username=postgres;Password=siddhi123456";
                _localConnectionString = configuration["DatabaseSettings:LocalConnectionString"] ?? defaultConn;
                _cloudConnectionString = configuration["DatabaseSettings:CloudConnectionString"] ?? "";
                _activeTarget = string.Equals(configuration["DatabaseSettings:ActiveTarget"], "Cloud", StringComparison.OrdinalIgnoreCase) ? "Cloud" : "Local";

                SaveConfigurationToFileInternal();
                _logger.LogInformation("Initialized database configuration from appsettings. Active target: {Target}", _activeTarget);
            }
        }
    }

    public string GetActiveConnectionString()
    {
        lock (_lock)
        {
            if (string.Equals(_activeTarget, "Cloud", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_cloudConnectionString))
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
            SaveConfigurationToFileInternal();
        }
    }

    public void UpdateLocalConnectionString(string connectionString)
    {
        lock (_lock)
        {
            _localConnectionString = connectionString?.Trim() ?? "";
            SaveConfigurationToFileInternal();
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
            // 3. Migrate control plane on target database
            await targetTenantDb.Database.MigrateAsync(ct);
            await targetTenantDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_name character varying(150);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_email character varying(150);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_mobile character varying(25);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS initial_password_hash character varying(255);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS state_code character varying(10);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS gstin character varying(20);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS address character varying(500);
                ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS rejection_reason character varying(500);

                CREATE TABLE IF NOT EXISTS tenant.feature_flags (
                    id uuid NOT NULL,
                    key character varying(100) NOT NULL,
                    name character varying(200) NOT NULL,
                    description text,
                    is_enabled boolean NOT NULL DEFAULT true,
                    category character varying(50) DEFAULT 'General',
                    tenant_id uuid,
                    created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    modified_at timestamp with time zone,
                    CONSTRAINT pk_feature_flags PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_feature_flags_key_tenant
                    ON tenant.feature_flags (key, COALESCE(tenant_id, '00000000-0000-0000-0000-000000000000'::uuid));

                INSERT INTO tenant.feature_flags (id, key, name, description, is_enabled, category, tenant_id, created_at)
                VALUES ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'voice_typing', 'Voice Typing & Speech Dictation', 'Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API', true, 'Accessibility', NULL, CURRENT_TIMESTAMP)
                ON CONFLICT DO NOTHING;
            ", ct);

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

        // 6. Persist target switch
        lock (_lock)
        {
            _activeTarget = normalizedTarget;
            SaveConfigurationToFileInternal();
        }

        _logger.LogInformation("Database switch to {Target} successful. {Count} schemas migrated.", normalizedTarget, migratedSchemaCount);

        return new DatabaseSwitchResult(
            Success: true,
            ActiveTarget: normalizedTarget,
            Message: $"Successfully pointed application to {normalizedTarget} PostgreSQL database. {migratedSchemaCount} active tenant schemas verified.",
            SchemasMigrated: migratedSchemaCount,
            Timestamp: DateTimeOffset.UtcNow);
    }

    private void SaveConfigurationToFileInternal()
    {
        try
        {
            var stored = new StoredDatabaseConfig
            {
                ActiveTarget = _activeTarget,
                LocalConnectionString = _localConnectionString,
                CloudConnectionString = _cloudConnectionString
            };

            var json = JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist database configuration to {Path}", _configFilePath);
        }
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

    private sealed class StoredDatabaseConfig
    {
        public string? ActiveTarget { get; set; }
        public string? LocalConnectionString { get; set; }
        public string? CloudConnectionString { get; set; }
    }
}

