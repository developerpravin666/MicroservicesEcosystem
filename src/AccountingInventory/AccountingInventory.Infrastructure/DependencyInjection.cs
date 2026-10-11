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
        services.AddSingleton<IDatabaseConnectionManager, DatabaseConnectionManager>();
        services.AddDbContext<AccountingInventoryDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var dbConnectionManager = sp.GetRequiredService<IDatabaseConnectionManager>();

            var connectionString = dbConnectionManager.GetActiveConnectionString();
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
            var dbConnectionManager = sp.GetRequiredService<IDatabaseConnectionManager>();
            var connectionString = dbConnectionManager.GetActiveConnectionString();

            options.UseNpgsql(
                       connectionString,
                       npgsql => npgsql.MigrationsHistoryTable("__ControlPlaneHistory", "tenant"))
                    .UseSnakeCaseNamingConvention()
                    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                    .AddInterceptors(sp.GetRequiredService<BuildingBlocks.Persistence.AuditableEntitySaveChangesInterceptor>());
        });
        services.AddScoped<ITenantDirectoryContext>(sp => sp.GetRequiredService<TenantDbContext>());

        services.AddScoped<ITenantSchemaProvisioner, TenantSchemaProvisionerAdapter>();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");
        services.AddSingleton(jwtOptions);
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
