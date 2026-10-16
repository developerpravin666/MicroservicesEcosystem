using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GodownId).IsRequired();
        builder.Property(x => x.ItemType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.LastMovementDate).HasColumnType("date").IsRequired();
        builder.HasIndex(x => x.GodownId);
        builder.HasIndex(x => new { x.GodownId, x.ProductId });
        builder.HasIndex(x => new { x.GodownId, x.SkuId });
        builder.HasIndex(x => x.SerialNumber);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

