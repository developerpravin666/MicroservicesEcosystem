using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class StockTransferJournalConfiguration : IEntityTypeConfiguration<StockTransferJournal>
{
    public void Configure(EntityTypeBuilder<StockTransferJournal> builder)
    {
        builder.ToTable("stock_transfer_journals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransferNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.TransferDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.FromGodownId).IsRequired();
        builder.Property(x => x.ToGodownId).IsRequired();
        builder.Property(x => x.VehicleNumber).HasMaxLength(50);
        builder.Property(x => x.DispatchNote).HasMaxLength(100);
        builder.Property(x => x.TransporterName).HasMaxLength(100);
        builder.Property(x => x.Narration).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.TotalQuantity).HasPrecision(18, 4);
        builder.Property(x => x.TotalValue).HasPrecision(18, 2);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.StockTransferJournalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TransferNumber).IsUnique();
        builder.HasIndex(x => x.TransferDate);
        builder.HasIndex(x => x.FromGodownId);
        builder.HasIndex(x => x.ToGodownId);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("stock_transfer_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StockTransferJournalId).IsRequired();
        builder.Property(x => x.ItemType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.TotalValue).HasPrecision(18, 2);

        builder.HasIndex(x => x.StockTransferJournalId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.SkuId);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

