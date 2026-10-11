using AccountingInventory.Domain.Entities;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

internal static class P0Configurations
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<BusinessRequest>().ToTable("business_requests");
        model.Entity<BusinessRequest>().HasKey(x => x.Id);
        model.Entity<BusinessRequest>().HasIndex(x => x.RequestKey).IsUnique();
        model.Entity<BusinessRequest>().Property(x => x.RequestKey).HasMaxLength(160);
        model.Entity<BusinessRequest>().Property(x => x.RequestHash).HasMaxLength(64);
        Configure<InvoiceCorrection>(model, "invoice_corrections");
        Configure<CorrectionRefund>(model, "correction_refunds");
        Configure<InvoiceSnapshot>(model, "invoice_snapshots");
        Configure<OpeningSubledgerBalance>(model, "opening_subledger_balances");
        Configure<OpeningSettlement>(model, "opening_settlements");
        model.Entity<InvoiceCorrection>().HasIndex(x => new { x.Kind, x.SourceId }).IsUnique();
        model.Entity<InvoiceCorrection>().HasIndex(x => x.NoteNumber).IsUnique();
        model.Entity<InvoiceSnapshot>().HasIndex(x => new { x.Kind, x.SourceId }).IsUnique();
        model.Entity<InvoiceSnapshot>().Property(x => x.DetailsJson).HasColumnType("jsonb");
        model.Entity<CorrectionRefund>().HasOne<InvoiceCorrection>().WithMany().HasForeignKey(x => x.CorrectionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OpeningSubledgerBalance>().HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OpeningSubledgerBalance>().HasIndex(x => new { x.Kind, x.PartyId, x.Reference }).IsUnique();
        model.Entity<OpeningSettlement>().HasOne<OpeningSubledgerBalance>().WithMany().HasForeignKey(x => x.OpeningBalanceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<User>().HasIndex(x => x.IsOwner).IsUnique().HasFilter("is_owner = true");
        model.Entity<User>().Property(x => x.InvitationHash).HasMaxLength(64);
        model.Entity<SalesProduct>().HasIndex(x => x.ProductId).IsUnique().HasFilter("is_deleted = false AND is_returned = false");
        Configure<SalesInvoiceReceipt>(model, "sales_invoice_receipts");
        Configure<CustomerAdvance>(model, "customer_advances");
        Configure<CustomerAdvanceRefund>(model, "customer_advance_refunds");
        model.Entity<SalesInvoiceReceipt>().HasOne<SalesInvoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SalesInvoiceReceipt>().HasOne<CustomerAdvance>().WithMany().HasForeignKey(x => x.AdvanceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CustomerAdvance>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CustomerAdvanceRefund>().HasOne<CustomerAdvance>().WithMany().HasForeignKey(x => x.AdvanceId).OnDelete(DeleteBehavior.Restrict);
        Configure<StockSku>(model, "stock_skus");
        Configure<SkuMovement>(model, "sku_movements");
        model.Entity<StockSku>().Property(s => s.Quantity).HasPrecision(18,4);
        model.Entity<StockSku>().Property(s => s.Code).HasMaxLength(40);
        model.Entity<StockSku>().HasIndex(s => s.Code).IsUnique();
        model.Entity<SkuMovement>().Property(s => s.Quantity).HasPrecision(18,4);
        model.Entity<SkuMovement>().HasOne<StockSku>().WithMany().HasForeignKey(s => s.SkuId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SkuMovement>().HasOne<Vendor>().WithMany().HasForeignKey(s => s.VendorId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SkuMovement>().HasIndex(s => new {s.SkuId,s.MovementDate});
        model.Entity<SalesInvoiceLine>().HasOne<StockSku>().WithMany().HasForeignKey(s => s.SkuId).OnDelete(DeleteBehavior.Restrict);
        Configure<SalesInvoice>(model, "sales_invoices");
        Configure<SalesInvoiceLine>(model, "sales_invoice_lines");
        model.Entity<SalesInvoice>().HasIndex(x => x.BillNumber).IsUnique();
        model.Entity<SalesInvoice>().HasIndex(x => x.CustomerId);
        model.Entity<SalesInvoice>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SalesInvoiceLine>().Property(x => x.Quantity).HasPrecision(18, 4);
        model.Entity<SalesInvoiceLine>().HasOne<SalesInvoice>().WithMany(x => x.Lines).HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
        Configure<MasterImportBatch>(model, "master_import_batches");
        Configure<MasterImportStagingRow>(model, "master_import_staging_rows");
        model.Entity<MasterImportBatch>().Property(x => x.BatchNumber).HasMaxLength(80).IsRequired();
        model.Entity<MasterImportBatch>().Property(x => x.FileName).HasMaxLength(250).IsRequired();
        model.Entity<MasterImportBatch>().Property(x => x.FileType).HasMaxLength(50);
        model.Entity<MasterImportBatch>().HasIndex(x => x.BatchNumber).IsUnique();
        model.Entity<MasterImportStagingRow>().Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        model.Entity<MasterImportStagingRow>().Property(x => x.EntityKey).HasMaxLength(150);
        model.Entity<MasterImportStagingRow>().Property(x => x.EntityName).HasMaxLength(250);
        model.Entity<MasterImportStagingRow>().HasOne<MasterImportBatch>().WithMany(b => b.Rows)
            .HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<MasterImportStagingRow>().HasIndex(x => new { x.BatchId, x.RowIndex });
    }
    private static void Configure<T>(ModelBuilder model, string table) where T : AggregateRoot
    {
        var builder = model.Entity<T>();
        builder.ToTable(table); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
        foreach (var property in typeof(T).GetProperties().Where(x => x.PropertyType == typeof(decimal)))
            builder.Property(property.Name).HasPrecision(18, 2);
    }
}
