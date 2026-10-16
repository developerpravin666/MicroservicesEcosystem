using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Abstractions;

/// <summary>
/// Schema-per-tenant data (backed by the "AccountingInventoryDb" database): Users and Roles for
/// the CURRENT tenant, whichever schema that resolves to via ITenantContext.
/// </summary>
public interface IAccountingInventoryDbContext
{
    DbSet<User> Users { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<FinanceVendor> FinanceVendors { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Variant> Variants { get; }
    DbSet<Color> Colors { get; }
    DbSet<ProductType> ProductTypes { get; }
    DbSet<ProductModel> ProductModels { get; }
    DbSet<StockSku> StockSkus { get; }
    DbSet<SkuMovement> SkuMovements { get; }
    DbSet<Product> Products { get; }
    DbSet<SalesProduct> SalesProducts { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceLine> SalesInvoiceLines { get; }
    DbSet<Customer> Customers { get; }
    DbSet<SalesPayment> SalesPayments { get; }
    DbSet<SalesReceipt> SalesReceipts { get; }
    DbSet<SalesInvoiceReceipt> SalesInvoiceReceipts { get; }
    DbSet<CustomerAdvance> CustomerAdvances { get; }
    DbSet<CustomerAdvanceRefund> CustomerAdvanceRefunds { get; }
    DbSet<CustomerBillSettings> CustomerBillSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<PurchasePayment> PurchasePayments { get; }
    DbSet<Tax> Taxes { get; }
    DbSet<ChartAccount> ChartAccounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<BankReconciliation> BankReconciliations { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }
    DbSet<InventoryAdjustment> InventoryAdjustments { get; }
    DbSet<AccountingApproval> AccountingApprovals { get; }
    DbSet<AccountingDimension> AccountingDimensions { get; }
    DbSet<SupportingDocument> SupportingDocuments { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<AccountBudget> AccountBudgets { get; }
    DbSet<AccountingUserPermission> AccountingUserPermissions { get; }
    DbSet<InvoiceCorrection> InvoiceCorrections { get; }
    DbSet<CorrectionRefund> CorrectionRefunds { get; }
    DbSet<InvoiceSnapshot> InvoiceSnapshots { get; }
    DbSet<OpeningSubledgerBalance> OpeningSubledgerBalances { get; }
    DbSet<OpeningSettlement> OpeningSettlements { get; }
    DbSet<MasterImportBatch> MasterImportBatches { get; }
    DbSet<MasterImportStagingRow> MasterImportStagingRows { get; }
    DbSet<Godown> Godowns { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<StockTransferJournal> StockTransferJournals { get; }
    DbSet<StockTransferLine> StockTransferLines { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    Task<string> GenerateSalesBillNumberAsync(int year, CancellationToken cancellationToken);

    //DbSet<Role> Roles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Forces the underlying connection closed so the next query opens a fresh one.
    /// Required immediately after resolving a tenant's schema mid-request (Register,
    /// Login) and calling ITenantContextAccessor.SetTenant — otherwise the connection
    /// already open from an earlier query keeps the OLD search_path. See
    /// TenantSchemaConnectionInterceptor.
    /// </summary>
    Task ResetConnectionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Shared, non-tenant-scoped data: the tenant registry itself (backed by its own,
/// separate "TenantDb" database — not a schema inside "AccountingInventoryDb"). This is where a
/// tenant's Name/Slug/SchemaName/Status live, reachable before any tenant schema is
/// known (Login/Register both resolve a slug through here first) and reachable no
/// matter how many per-tenant schemas exist in the other database.
/// </summary>
public interface ITenantDirectoryContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
