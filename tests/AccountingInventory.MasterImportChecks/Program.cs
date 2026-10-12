using System.Text.Json;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AccountingInventory.Application;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.MasterImport;
using AccountingInventory.Infrastructure;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using AccountingInventory.Api.Endpoints;

if (!args.Contains("--local")) throw new InvalidOperationException("Use --local to check the local development database.");
var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repository,
    "src/AccountingInventory/AccountingInventory.Api/appsettings.json")));
var connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("AccountingInventoryDb").GetString()!;
var connectionSettings = new NpgsqlConnectionStringBuilder(connectionString);
if (connectionSettings.Host is not ("localhost" or "127.0.0.1" or "::1")) throw new InvalidOperationException("A local PostgreSQL host is required.");
var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings {
    ContentRootPath = Path.Combine(repository, "src/AccountingInventory/AccountingInventory.Api"),
    EnvironmentName = "Development",
});
hostBuilder.Logging.ClearProviders();
hostBuilder.Services.AddRouting();
hostBuilder.Services.AddPlatformSecurity(hostBuilder.Configuration);
hostBuilder.Services.AddApplication();
hostBuilder.Services.AddInfrastructure(hostBuilder.Configuration);
using var host = hostBuilder.Build();
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var accessor = services.GetRequiredService<ITenantContextAccessor>();
    accessor.SetTenant(Guid.Empty, "tenant");
    var runtimeDb = services.GetRequiredService<IAccountingInventoryDbContext>();
    var imports = services.GetRequiredService<IMasterDataImportService>();
    var directory = services.GetRequiredService<ITenantDirectoryContext>();
    var target = await directory.Tenants.SingleAsync(t => t.Id == Guid.Parse("54f8e124-cc23-448f-8ee0-1c9604e930ac"));
    Console.WriteLine($"Runtime selected tenant: {target.SchemaName}, active={target.IsActive}");
    accessor.SetTenant(target.Id, target.SchemaName);
    await runtimeDb.ResetConnectionAsync(CancellationToken.None);
    var concreteDb = (AccountingInventoryDbContext)runtimeDb;
    await concreteDb.Database.OpenConnectionAsync();
    await using var pathCommand = concreteDb.Database.GetDbConnection().CreateCommand();
    pathCommand.CommandText = "SHOW search_path";
    Console.WriteLine($"Runtime search path: {await pathCommand.ExecuteScalarAsync()}");
    var http = new DefaultHttpContext { RequestServices = services };
    http.Request.Method = "GET";
    http.Request.RouteValues["tenantId"] = target.Id;
    http.Request.Path = $"/api/admin/tenants/{target.Id}/masters/import/batches";
    var filterContext = new DefaultEndpointFilterInvocationContext(http, []);
    var filter = new AdminImportStoreEndpointFilter();
    var queried = false;
    var result = await filter.InvokeAsync(filterContext, async _ => {
        queried = true;
        return Results.Ok(await imports.GetBatchesAsync(CancellationToken.None));
    });
    if (queried || result is not IValueHttpResult { Value: MasterImportBatchSummaryDto[] { Length: 0 } })
        throw new Exception("Unprovisioned tenant must return an empty batch list without querying its store.");
    Console.WriteLine("PASS: exact failing tenant returns empty batches without querying nonexistent tables.");
    http.Request.Path = $"/api/admin/tenants/{target.Id}/masters/import/export";
    result = await filter.InvokeAsync(filterContext, _ => throw new Exception("Unprovisioned store export must not run."));
    if (result is not IStatusCodeHttpResult { StatusCode: 409 }) throw new Exception("Expected store-not-ready response.");
    Console.WriteLine("PASS: unprovisioned tenant export returns 409 with provisioning guidance.");
    var activeTarget = await directory.Tenants.FirstAsync(t => t.IsActive && !t.IsDeleted);
    accessor.SetTenant(activeTarget.Id, activeTarget.SchemaName);
    await runtimeDb.ResetConnectionAsync(CancellationToken.None);
    http.Request.RouteValues["tenantId"] = activeTarget.Id;
    http.Request.Path = $"/api/admin/tenants/{activeTarget.Id}/masters/import/batches";
    queried = false;
    await filter.InvokeAsync(filterContext, async _ => {
        queried = true;
        return Results.Ok(await imports.GetBatchesAsync(CancellationToken.None));
    });
    if (!queried) throw new Exception("Provisioned tenant imports should run normally.");
    Console.WriteLine("PASS: full runtime DI imports still work for provisioned tenants.");
}
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
var schemas = new List<string>();
await using (var command = new NpgsqlCommand("SELECT DISTINCT schema_name FROM tenant.tenants WHERE is_active AND NOT is_deleted", connection))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) schemas.Add(reader.GetString(0));
foreach (var schema in schemas)
{
    await using var check = new NpgsqlCommand("SELECT to_regclass(format('%I.master_import_batches', @schema)) IS NOT NULL, to_regclass(format('%I.master_import_staging_rows', @schema)) IS NOT NULL", connection);
    check.Parameters.AddWithValue("schema", schema);
    await using (var reader = await check.ExecuteReaderAsync())
    {
        await reader.ReadAsync();
        Console.WriteLine($"{schema}: batches={reader.GetBoolean(0)}, staging rows={reader.GetBoolean(1)}");
    }
    if (args.Contains("--repair"))
        await TenantSchemaMigrator.MigrateAsync(connectionString, schema, CancellationToken.None);
    var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
        .UseNpgsql(TenantSchemaMigrator.CreateTenantConnectionString(connectionString, schema))
        .UseSnakeCaseNamingConvention().Options;
    await using var db = new AccountingInventoryDbContext(options, new StaticTenantProvider(schema));
    await db.MasterImportBatches.AsNoTracking().Take(1).ToListAsync();
    await db.MasterImportStagingRows.AsNoTracking().Take(1).ToListAsync();
    Console.WriteLine($"PASS: {schema} import batches and staging rows are readable through EF.");

    // Reproduce admin handlers: the context starts on the control plane, and the
    // selected tenant is established after the context and service are resolved.
    var provider = new SwitchingTenantProvider { SchemaName = "tenant" };
    var adminOptions = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
        .UseNpgsql(TenantSchemaMigrator.CreateTenantConnectionString(connectionString, "tenant"))
        .UseSnakeCaseNamingConvention().Options;
    await using var adminDb = new AccountingInventoryDbContext(adminOptions, provider);
    await adminDb.Database.OpenConnectionAsync();
    provider.SchemaName = schema;
    await adminDb.ResetConnectionAsync(CancellationToken.None);
    await adminDb.MasterImportBatches.AsNoTracking().Take(1).ToListAsync();
    await adminDb.MasterImportStagingRows.AsNoTracking().Take(1).ToListAsync();
    Console.WriteLine($"PASS: admin context switches from control plane to {schema} before reading imports.");
}

sealed class SwitchingTenantProvider : ITenantProvider
{
    public string SchemaName { get; set; } = "tenant";
}
