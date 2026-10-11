using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class SalesReceiptConfiguration : IEntityTypeConfiguration<SalesReceipt>
{
    public void Configure(EntityTypeBuilder<SalesReceipt> builder)
    {
        builder.ToTable("sales_receipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Amount).HasPrecision(18, 2);
        builder.Property(receipt => receipt.PaymentMode).IsRequired().HasMaxLength(30);
        builder.Property(receipt => receipt.PaymentDate).HasColumnType("date");
        builder.Property(receipt => receipt.ReferenceNumber).HasMaxLength(100);
        builder.Property(receipt => receipt.Note).HasMaxLength(500);
        builder.HasIndex(receipt => new { receipt.SalesProductId, receipt.PaymentDate });
        builder.HasOne<SalesProduct>().WithMany().HasForeignKey(receipt => receipt.SalesProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(receipt => receipt.DomainEvents);
        builder.HasQueryFilter(receipt => !receipt.IsDeleted);
    }
}
