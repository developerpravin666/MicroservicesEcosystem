using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class EWayBillConfiguration : IEntityTypeConfiguration<EWayBill>
{
    public void Configure(EntityTypeBuilder<EWayBill> builder)
    {
        builder.ToTable("eway_bills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EwbNumber).HasMaxLength(20);
        builder.Property(x => x.DocType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.SupplyType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.SubSupplyType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.TransporterId).HasMaxLength(30);
        builder.Property(x => x.TransporterName).HasMaxLength(150);
        builder.Property(x => x.TransDocNo).HasMaxLength(50);
        builder.Property(x => x.VehicleNo).HasMaxLength(30);
        builder.Property(x => x.VehicleType).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CancelReason).HasMaxLength(250);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.SalesInvoiceId);
        builder.HasIndex(x => x.EwbNumber);
        builder.HasOne<SalesInvoice>().WithMany().HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

