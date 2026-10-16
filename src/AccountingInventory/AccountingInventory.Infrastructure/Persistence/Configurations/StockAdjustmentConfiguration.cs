using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AdjustmentNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.AdjustmentDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.GodownId).IsRequired();
        builder.Property(x => x.AdjustmentType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(300);
        builder.Property(x => x.ItemType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.BookQuantity).HasPrecision(18, 4);
        builder.Property(x => x.PhysicalQuantity).HasPrecision(18, 4);
        builder.Property(x => x.DiscrepancyQuantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.TotalValue).HasPrecision(18, 2);

        builder.HasIndex(x => x.AdjustmentNumber).IsUnique();
        builder.HasIndex(x => x.AdjustmentDate);
        builder.HasIndex(x => x.GodownId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.SkuId);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

