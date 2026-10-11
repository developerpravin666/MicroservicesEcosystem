using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        // Lives in the "tenant" schema — the master/control-plane schema — inside
        // the SAME PostgreSQL database as every tenant's own schema (e.g.
        // "tenant_acme_3f2a1b4c"). One database, many schemas: "tenant" holds shared
        // data (the registry itself), each "tenant_<name>_<id>" schema holds that
        // one tenant's Users/Roles. An explicit schema here always wins over
        // whatever search_path the current request set, so this table stays
        // reachable no matter which tenant (if any) is in context.
        builder.ToTable(Constants.DBConstants.DBTableNames.Tenants, schema: "tenant");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(48);
        builder.Property(t => t.SchemaName).IsRequired().HasMaxLength(63);
        builder.Property(t => t.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.OwnerName).HasMaxLength(150);
        builder.Property(t => t.OwnerEmail).HasMaxLength(150);
        builder.Property(t => t.OwnerMobile).HasMaxLength(25);
        builder.Property(t => t.InitialPasswordHash).HasMaxLength(255);
        builder.Property(t => t.StateCode).HasMaxLength(10);
        builder.Property(t => t.Gstin).HasMaxLength(20);
        builder.Property(t => t.Address).HasMaxLength(500);
        builder.Property(t => t.RejectionReason).HasMaxLength(500);

        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.SchemaName).IsUnique();

        builder.Ignore(t => t.DomainEvents);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}