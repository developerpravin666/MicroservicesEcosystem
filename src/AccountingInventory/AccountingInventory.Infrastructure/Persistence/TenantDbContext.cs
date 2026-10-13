using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountingInventory.Infrastructure.Persistence;

/// <summary>
/// The shared tenant registry — lives in the "tenant" schema of the SAME
/// "AccountingInventoryDb" PostgreSQL database that every tenant's own schema
/// (IdentityDbContext) also lives in. One database, multiple schemas:
///   - "tenant"                     → this context: the registry itself (shared/master data)
///   - "tenant_acme_3f2a1b4c", ...  → IdentityDbContext, one schema per tenant
/// Migrated once at application startup, independent of any tenant's schema
/// migrations (see TenantSchemaProvisioner, used from RegisterTenantCommandHandler) —
/// its own, separately-named migrations history table guarantees that even though
/// both contexts share a database, their migration sets can never collide.
/// </summary>
public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options)
    : DbContext(options), ITenantDirectoryContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<TableRegistry> TableRegistries => Set<TableRegistry>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.Entity<FeatureFlag>(b =>
        {
            b.ToTable("feature_flags", "tenant");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.Description).HasColumnType("text");
            b.Property(x => x.IsEnabled).HasDefaultValue(true);
            b.HasIndex(x => new { x.Key, x.TenantId }).IsUnique();
        });
        modelBuilder.Entity<TableRegistry>(b =>
        {
            b.ToTable("table_registry", "tenant");
            b.HasKey(x => x.Id);
            b.Property(x => x.SchemaName).HasMaxLength(100).IsRequired();
            b.Property(x => x.TableName).HasMaxLength(100).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            b.Property(x => x.TableType).HasMaxLength(50).IsRequired().HasDefaultValue("Data");
            b.Property(x => x.Category).HasMaxLength(100).HasDefaultValue("General");
            b.Property(x => x.Description).HasColumnType("text");
            b.Property(x => x.IsSystem).HasDefaultValue(false);
            b.HasIndex(x => new { x.SchemaName, x.TableName }).IsUnique();
        });
        base.OnModelCreating(modelBuilder);
    }
}