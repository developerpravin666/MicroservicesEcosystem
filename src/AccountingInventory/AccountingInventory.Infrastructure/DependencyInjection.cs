using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using AccountingInventory.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AccountingInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // "AccountingInventoryDb": one PostgreSQL database, multiple schemas.
        //   - "tenant" schema        → TenantDbContext: the shared tenant registry (master data)
        //   - "tenant_<name>_<id>"    → IdentityDbContext: Users/Roles, one schema per tenant,
        //                               named from that tenant's name + id (see
        //                               Tenant.Create / TenantSchemaNameValidator)
        services.AddHttpContextAccessor();
        services.AddScoped<BuildingBlocks.Persistence.ICurrentUserProvider, BuildingBlocks.Persistence.HttpCurrentUserProvider>();
        services.AddScoped<IRequestIdentity, RequestIdentity>();
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(BusinessTransactionBehavior<,>));
        services.AddScoped<ITenantProvider, DynamicTenantProvider>();
        services.AddScoped<TenantSchemaConnectionInterceptor>();
        var connectionString = configuration.GetConnectionString("AccountingInventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");
        services.AddDbContext<AccountingInventoryDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();

            var tenantConnectionString = TenantSchemaMigrator.CreateTenantConnectionString(connectionString, tenantProvider.SchemaName);

            options.UseNpgsql(
                       tenantConnectionString,
                   npgsql => npgsql.MigrationsHistoryTable("__TenantSchemaHistory", tenantProvider.SchemaName))
                   .UseSnakeCaseNamingConvention()
                   .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .AddInterceptors(sp.GetRequiredService<TenantSchemaConnectionInterceptor>());
        });
        services.AddScoped<IAccountingInventoryDbContext>(sp => sp.GetRequiredService<AccountingInventoryDbContext>());

        services.AddScoped<BuildingBlocks.Persistence.AuditableEntitySaveChangesInterceptor>();
        services.AddDbContext<TenantDbContext>((sp, options) =>
        {

            options.UseNpgsql(
                       connectionString,
                       npgsql => npgsql.MigrationsHistoryTable("__ControlPlaneHistory", "tenant"))
                    .UseSnakeCaseNamingConvention()
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                    .AddInterceptors(sp.GetRequiredService<BuildingBlocks.Persistence.AuditableEntitySaveChangesInterceptor>());
        });
        services.AddScoped<ITenantDirectoryContext>(sp => sp.GetRequiredService<TenantDbContext>());

        services.AddScoped<ITenantSchemaProvisioner, TenantSchemaProvisionerAdapter>();
        services.AddScoped<IDatabaseMigrationCoordinator, DatabaseMigrationCoordinator>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");
        services.AddSingleton(jwtOptions);
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ITenantAdministration, TenantAdministration>();
        services.AddScoped<IRegistrationEmailTemplateStore, RegistrationEmailTemplateStore>();
        services.AddScoped<AccountingInventory.Application.DatabaseBackup.Abstractions.IDatabaseBackupService, AccountingInventory.Infrastructure.Persistence.Services.DatabaseBackupService>();
        services.AddHttpClient();
        services.AddScoped<AccountingInventory.Application.OCR.IOcrProviderEngine, AccountingInventory.Infrastructure.OCR.Engines.LocalTesseractOcrEngine>();
        services.AddScoped<AccountingInventory.Application.OCR.IOcrProviderEngine, AccountingInventory.Infrastructure.OCR.Engines.AzureDocumentIntelligenceOcrEngine>();
        services.AddScoped<AccountingInventory.Application.OCR.IOcrProviderEngine, AccountingInventory.Infrastructure.OCR.Engines.GoogleDocumentAiOcrEngine>();
        services.AddScoped<AccountingInventory.Application.OCR.IOcrService, AccountingInventory.Infrastructure.OCR.Services.OcrService>();

        return services;

    }
}
