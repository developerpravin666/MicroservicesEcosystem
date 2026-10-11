using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class ProductModelConfiguration : IEntityTypeConfiguration<ProductModel>
{
    public void Configure(EntityTypeBuilder<ProductModel> builder)
    {
        // The Npgsql connection Search Path selects the product_types schema. Keeping this
        // unqualified is essential: the same migration is reused for every product_types.
        builder.ToTable(Constants.DBConstants.DBTableNames.ProductModels);
        builder.HasKey(u => u.Id);

        builder.Property(u => u.BrandId).IsRequired();
        builder.Property(u => u.ProductTypeId).IsRequired();
        builder.Property(u => u.Name).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Code).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Description).HasMaxLength(500);

        builder.HasIndex(u => new { u.BrandId, u.ProductTypeId, u.Name }).IsUnique();

        builder.Ignore(u => u.DomainEvents);
        builder.HasQueryFilter(u => !u.IsDeleted);

    }
}
