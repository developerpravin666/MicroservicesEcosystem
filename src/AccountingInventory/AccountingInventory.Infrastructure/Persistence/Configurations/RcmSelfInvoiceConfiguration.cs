using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class RcmSelfInvoiceConfiguration : IEntityTypeConfiguration<RcmSelfInvoice>
{
    public void Configure(EntityTypeBuilder<RcmSelfInvoice> builder)
    {
        builder.ToTable("rcm_self_invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VoucherNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PaymentVoucherNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.VendorName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.VendorGstin).HasMaxLength(20);
        builder.Property(x => x.Section).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ServiceCategory).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SupplierInvoiceNumber).HasMaxLength(100);
        builder.Property(x => x.PlaceOfSupplyStateCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.PlaceOfSupplyStateName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.HsnSacCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ItemDescription).IsRequired().HasMaxLength(300);
        builder.Property(x => x.TaxableAmount).HasPrecision(18, 2);
        builder.Property(x => x.GstRate).HasPrecision(6, 2);
        builder.Property(x => x.CgstRate).HasPrecision(6, 2);
        builder.Property(x => x.SgstRate).HasPrecision(6, 2);
        builder.Property(x => x.IgstRate).HasPrecision(6, 2);
        builder.Property(x => x.CgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.SgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.IgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.PaymentReference).HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CancelReason).HasMaxLength(300);
        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => x.VoucherNumber);
        builder.HasIndex(x => x.VoucherDate);
        builder.HasIndex(x => x.VendorId);
        builder.HasIndex(x => x.Status);

        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

