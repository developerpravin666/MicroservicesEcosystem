using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // The Npgsql connection Search Path selects the tenant schema. Keeping this
        // unqualified is essential: the same migration is reused for every tenant.
        builder.ToTable(Constants.DBConstants.DBTableNames.Users);
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName).IsRequired().HasMaxLength(64);
        builder.Property(u => u.Mobile).IsRequired().HasMaxLength(20);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired();

        // Uniqueness only needs to hold WITHIN a schema — since schema-per-tenant
        // already guarantees physical separation between tenants, a plain unique
        // index (not a composite with TenantId) is correct and sufficient here.
        builder.HasIndex(u => u.UserName).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        //builder.HasIndex(u => u.Mobile).IsUnique();

        //builder.Property<List<Guid>>("_roleIds")
        //    .HasField("_roleIds")
        //    .UsePropertyAccessMode(PropertyAccessMode.Field)
        //    .HasColumnName("RoleIds")
        //    .HasConversion(
        //        roleIds => string.Join(',', roleIds),
        //        value => value.Length == 0
        //            ? new List<Guid>()
        //            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        builder.Property(u => u.RefreshTokenHash).HasMaxLength(512);
        builder.Property(u => u.RefreshTokenExpiresAtUtc);

        builder.Ignore(u => u.DomainEvents);
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
