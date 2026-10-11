using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class SalesPaymentConfiguration : IEntityTypeConfiguration<SalesPayment>
{
    public void Configure(EntityTypeBuilder<SalesPayment> builder)
    {
        builder.ToTable(Constants.DBConstants.DBTableNames.SalesPayments);
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.PaymentMode).IsRequired().HasMaxLength(20);
        builder.Property(payment => payment.DownPayment).HasPrecision(18, 2);
        builder.Property(payment => payment.EmiAmount).HasPrecision(18, 2);
        builder.Property(payment => payment.InsuranceAmount).HasPrecision(18, 2);
        builder.Property(payment => payment.FirstInstallmentDate).HasColumnType("date");
        builder.HasIndex(payment => payment.SalesProductId).IsUnique();
        builder.HasIndex(payment => payment.FinanceVendorId);
        builder.HasOne<SalesProduct>().WithMany()
            .HasForeignKey(payment => payment.SalesProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FinanceVendor>().WithMany()
            .HasForeignKey(payment => payment.FinanceVendorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(payment => payment.DomainEvents);
        builder.HasQueryFilter(payment => !payment.IsDeleted);
    }
}
