using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.WebDefaults;
using AccountingInventory.Api.Endpoints;
using AccountingInventory.Application;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Infrastructure;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "AccountingInventory.Api";
var builder = WebApplication.CreateBuilder(args);
AccountingInventory.Api.ProductionConfiguration.Validate(builder);

builder.AddSharedLogging(ServiceName);

builder.Services.AddPlatformSecurity(builder.Configuration);
builder.Services.AddWebDefaults(ServiceName);
bool isMessaging = builder.Configuration.GetSection("RabbitMq:Enable").Get<bool>();
if (isMessaging)
{
    builder.Services.AddPlatformMessaging(builder.Configuration, AccountingInventory.Application.DependencyInjection.ApplicationAssembly);
}
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        var origins = new List<string>();
        var frontendUrl = builder.Configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            origins.Add(frontendUrl.TrimEnd('/'));
        }
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        origins.AddRange(configuredOrigins);

        if (builder.Environment.IsDevelopment())
        {
            origins.Add("http://localhost:5173");
            origins.Add("http://localhost:3000");
        }

        var distinctOrigins = origins.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToArray();
        if (distinctOrigins.Length > 0)
        {
            policy.WithOrigins(distinctOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseWebDefaults();
app.UseCorrelationId();
app.UseCors("FrontendCorsPolicy");
app.UseSharedRequestLogging();
app.UsePlatformSecurity();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapEmailEndpoints();
//app.MapRoleEndpoints();
app.MapTenantEndpoints();
app.MapVendorEndpoints();
app.MapFinanceVendorEndpoints();
app.MapBrandEndpoints();
app.MapVariantEndpoints();
app.MapColorEndpoints();
app.MapProductTypeEndpoints();
app.MapProductModelEndpoints();
app.MapMasterImportEndpoints();
app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapPurchaseAccountingEndpoints();
app.MapSalesProductEndpoints();
app.MapSalesAccountingEndpoints();
app.MapSalesInvoiceEndpoints();
app.MapCustomerEndpoints();
app.MapCustomerBillSettingsEndpoints();
app.MapAuditLogEndpoints();
app.MapGeneralLedgerEndpoints();
app.MapAccountingExtensionEndpoints();
app.MapAccountingP0Endpoints();
app.MapTaxEndpoints();
app.MapComplianceEndpoints();
app.MapDashboardEndpoints();
app.MapFeatureFlagEndpoints();
app.MapTableRegistryEndpoints();
app.MapDatabaseBackupEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = ServiceName }));
app.MapGet("/health/ready", async (TenantDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Tenants.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var coordinator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationCoordinator>();
    var result = await coordinator.MigrateAllAsync();
    if (!result.Success)
    {
        Log.Error("Platform database migrations failed: {Message}. Schemas with issues: {Count}",
            result.Message, result.FailedSchemas.Count);
        throw new InvalidOperationException("Database migrations failed. Review migration logs before starting the API.");
    }
    else
    {
        Log.Information("Platform database migrations completed successfully across {Count} schemas.", result.SuccessfulSchemas);
    }
}

Log.Information("Starting {Service}", ServiceName);
app.Run();
