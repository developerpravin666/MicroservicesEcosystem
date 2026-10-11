using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class PurchasePaymentConfiguration : IEntityTypeConfiguration<PurchasePayment>
{
    public void Configure(EntityTypeBuilder<PurchasePayment> builder)
    {
        builder.ToTable("purchase_payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.BillNumber).IsRequired().HasMaxLength(100);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PaymentMode).IsRequired().HasMaxLength(30);
        builder.Property(payment => payment.PaymentDate).HasColumnType("date");
        builder.Property(payment => payment.ReferenceNumber).HasMaxLength(100);
        builder.Property(payment => payment.Note).HasMaxLength(500);
        builder.HasIndex(payment => new { payment.VendorId, payment.BillNumber });
        builder.HasOne<Vendor>().WithMany().HasForeignKey(payment => payment.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(payment => payment.DomainEvents);
        builder.HasQueryFilter(payment => !payment.IsDeleted);
    }
}
