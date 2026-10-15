using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        // The Npgsql connection Search Path selects the tenant schema. Keeping this
        // unqualified is essential: the same migration is reused for every tenant.
        builder.ToTable(Constants.DBConstants.DBTableNames.Vendors);
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).IsRequired().HasMaxLength(64);
        builder.Property(u => u.Mobile).HasMaxLength(20);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.Code).IsRequired();
        builder.Property(u => u.GstNumber).HasMaxLength(20);

        builder.HasIndex(u => u.Code).IsUnique();

        builder.Ignore(u => u.DomainEvents);
        builder.HasQueryFilter(u => !u.IsDeleted);

    }
}
