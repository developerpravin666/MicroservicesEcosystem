using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Shared registration helpers so every schema-per-tenant DbContext in every service
/// is wired up identically: same audit interceptor, same tenant-schema connection
/// interceptor, same Npgsql provider. A new service cloned from the Gold Master calls
/// this one method instead of hand-assembling AddDbContext + AddInterceptors itself.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers a schema-per-tenant DbContext of type <typeparamref name="TContext"/>
    /// against the connection string named <paramref name="connectionStringName"/>,
    /// with the audit + tenant-schema interceptors and a TenantSchemaProvisioner
    /// already wired up. The DbContext's own OnModelCreating must NOT call
    /// HasDefaultSchema — schema selection happens purely via PostgreSQL's
    /// search_path (see TenantSchemaConnectionInterceptor).
    ///
    /// Uses a distinctly-named migrations history table ("__TenantSchemaHistory",
    /// left unqualified so it floats with search_path just like the entity tables) —
    /// this matters when a service also has a control-plane DbContext
    /// (AddControlPlaneDbContext) pointed at the SAME physical database: without
    /// distinct history table names, both DbContexts' completely different migration
    /// sets would collide in one shared "__EFMigrationsHistory" table.
    /// </summary>
    public static IServiceCollection AddTenantScopedDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string connectionStringName)
        where TContext : DbContext
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, HttpCurrentUserProvider>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<TenantSchemaConnectionInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
            options.UseNpgsql(
                       configuration.GetConnectionString(connectionStringName),
                       npgsql => npgsql.MigrationsHistoryTable("__TenantSchemaHistory"))
            .UseSnakeCaseNamingConvention()
                   .AddInterceptors(
                       sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                       sp.GetRequiredService<TenantSchemaConnectionInterceptor>()));

        services.AddScoped<TenantSchemaProvisioner<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers a DbContext for a service's control-plane data — data that is NOT
    /// tenant-scoped (e.g. AccountingInventory's tenant registry). Gets the shared audit
    /// interceptor (created/modified stamps, soft delete) but deliberately NOT the
    /// tenant-schema connection interceptor, since control-plane tables always live
    /// in one fixed schema regardless of which tenant the caller belongs to.
    ///
    /// Shares the SAME physical database (and connection string) as any
    /// AddTenantScopedDbContext-registered context for the same service — this is
    /// one PostgreSQL database with multiple schemas, not multiple databases. The
    /// control-plane schema (default "tenant") holds the shared/master data; each
    /// tenant then gets its own additional schema (e.g. "tenant_acme_3f2a1b4c") for
    /// its own tenant-scoped tables. Uses its own distinctly-named, schema-pinned
    /// migrations history table so its independent migration set can never collide
    /// with the tenant-scoped context's — see AddTenantScopedDbContext.
    /// </summary>
    public static IServiceCollection AddControlPlaneDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName,
        string schema = "tenant")
        where TContext : DbContext
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, HttpCurrentUserProvider>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
            options.UseNpgsql(
                       configuration.GetConnectionString(connectionStringName),
                       npgsql => npgsql.MigrationsHistoryTable("__ControlPlaneHistory", schema))
                    .UseSnakeCaseNamingConvention()
                   .AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>()));

        return services;
    }
}