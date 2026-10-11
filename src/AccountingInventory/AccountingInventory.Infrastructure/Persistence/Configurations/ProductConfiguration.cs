using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(Constants.DBConstants.DBTableNames.Products);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SerialNumber1).HasMaxLength(100);
        builder.Property(x => x.BillNumber).HasMaxLength(100);
        builder.Property(x => x.PurchaseDate).HasColumnType("date");
        builder.Property(x => x.DueDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.PaymentTermsDays).IsRequired();
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.IsSold).IsConcurrencyToken();
        builder.Property(x => x.Discount).HasPrecision(18, 2);
        builder.Property(x => x.Cgst).HasPrecision(5, 2);
        builder.Property(x => x.Sgst).HasPrecision(5, 2);
        builder.Property(x => x.Tax).HasPrecision(5, 2);
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
