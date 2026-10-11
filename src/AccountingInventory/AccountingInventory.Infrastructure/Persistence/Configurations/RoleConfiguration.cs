using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Also unqualified/schema-less — lives in the current tenant's schema.
        builder.ToTable(Constants.DBConstants.DBTableNames.Roles);
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.IsSystemDefined).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.Property<List<string>>("_permissionCodes")
            .HasField("_permissionCodes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("PermissionCodes")
            .HasConversion(
                codes => string.Join('|', codes),
                value => value.Length == 0
                    ? new List<string>()
                    : value.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList());

        builder.Ignore(r => r.DomainEvents);
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
