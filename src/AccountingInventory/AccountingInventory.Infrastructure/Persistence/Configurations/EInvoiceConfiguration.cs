using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class EInvoiceConfiguration : IEntityTypeConfiguration<EInvoice>
{
    public void Configure(EntityTypeBuilder<EInvoice> builder)
    {
        builder.ToTable("einvoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Irn).IsRequired().HasMaxLength(64);
        builder.Property(x => x.AckNo).IsRequired().HasMaxLength(30);
        builder.Property(x => x.SignedQrCode).IsRequired().HasColumnType("text");
        builder.Property(x => x.SignedInvoice).HasColumnType("text");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CancelReason).HasMaxLength(250);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.SalesInvoiceId);
        builder.HasIndex(x => x.Irn).IsUnique();
        builder.HasIndex(x => x.AckNo);
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

