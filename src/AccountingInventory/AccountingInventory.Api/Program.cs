using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.WebDefaults;
using AccountingInventory.Api.Endpoints;
using AccountingInventory.Application;
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
builder.Services.AddPlatformMessaging(builder.Configuration, AccountingInventory.Application.DependencyInjection.ApplicationAssembly);

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
app.MapDashboardEndpoints();
app.MapFeatureFlagEndpoints();
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

using (var scope = app.Services.CreateScope())
{
    var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await tenantDb.Database.MigrateAsync();
    await tenantDb.Database.ExecuteSqlRawAsync(@"
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
    ");
}

await app.Services.ApplyTenantSchemaMigrationsAsync();

Log.Information("Starting {Service}", ServiceName);
app.Run();
