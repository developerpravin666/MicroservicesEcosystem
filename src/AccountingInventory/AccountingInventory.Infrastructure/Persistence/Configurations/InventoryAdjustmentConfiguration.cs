using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("inventory_adjustments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AdjustmentDate).HasColumnType("date");
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(300);
        builder.Property(item => item.Cost).HasPrecision(18, 2);
        builder.HasIndex(item => item.ProductId);
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(item => item.DomainEvents);
        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}
