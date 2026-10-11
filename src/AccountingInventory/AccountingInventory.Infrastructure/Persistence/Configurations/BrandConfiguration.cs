using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        // The Npgsql connection Search Path selects the brand schema. Keeping this
        // unqualified is essential: the same migration is reused for every brand.
        builder.ToTable(Constants.DBConstants.DBTableNames.Brands);
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Description).HasMaxLength(500);

        builder.HasIndex(u => u.Name).IsUnique();

        //builder.Ignore(u => u.DomainEvents);
        builder.HasQueryFilter(u => !u.IsDeleted);

    }
}
