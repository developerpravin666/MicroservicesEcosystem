using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence.Configurations;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using BuildingBlocks.Domain;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace AccountingInventory.Infrastructure.Persistence;

/// <summary>
/// Schema-per-tenant: Users and Vendors. The active schema is supplied by
/// <see cref="ITenantProvider"/> and is part of the EF model cache key.
///
/// Configurations are applied explicitly (NOT via ApplyConfigurationsFromAssembly)
/// because this project's assembly also contains TenantConfiguration, which belongs
/// exclusively to TenantDbContext (a separate database entirely — see
/// DependencyInjection.AddControlPlaneDbContext) — an assembly-wide scan here would
/// incorrectly pull that unrelated table into this context's migrations too.
/// </summary>
public sealed class AccountingInventoryDbContext(
    DbContextOptions<AccountingInventoryDbContext> options,
    ITenantProvider tenantProvider,
    ICurrentUserProvider? currentUserProvider = null)
    : DbContext(options), IAccountingInventoryDbContext
{
    public string SchemaName => tenantProvider.SchemaName;

    public DbSet<User> Users => Set<User>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<FinanceVendor> FinanceVendors => Set<FinanceVendor>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Variant> Variants => Set<Variant>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();
    public DbSet<StockSku> StockSkus => Set<StockSku>();
    public DbSet<SkuMovement> SkuMovements => Set<SkuMovement>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SalesProduct> SalesProducts => Set<SalesProduct>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesPayment> SalesPayments => Set<SalesPayment>();
    public DbSet<SalesReceipt> SalesReceipts => Set<SalesReceipt>();
    public DbSet<SalesInvoiceReceipt> SalesInvoiceReceipts => Set<SalesInvoiceReceipt>();
    public DbSet<CustomerAdvance> CustomerAdvances => Set<CustomerAdvance>();
    public DbSet<CustomerAdvanceRefund> CustomerAdvanceRefunds => Set<CustomerAdvanceRefund>();
    public DbSet<CustomerBillSettings> CustomerBillSettings => Set<CustomerBillSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<ChartAccount> ChartAccounts => Set<ChartAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<AccountingApproval> AccountingApprovals => Set<AccountingApproval>();
    public DbSet<AccountingDimension> AccountingDimensions => Set<AccountingDimension>();
    public DbSet<SupportingDocument> SupportingDocuments => Set<SupportingDocument>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<AccountBudget> AccountBudgets => Set<AccountBudget>();
    public DbSet<AccountingUserPermission> AccountingUserPermissions => Set<AccountingUserPermission>();
    public DbSet<InvoiceCorrection> InvoiceCorrections => Set<InvoiceCorrection>();
    public DbSet<CorrectionRefund> CorrectionRefunds => Set<CorrectionRefund>();
    public DbSet<InvoiceSnapshot> InvoiceSnapshots => Set<InvoiceSnapshot>();
    public DbSet<OpeningSubledgerBalance> OpeningSubledgerBalances => Set<OpeningSubledgerBalance>();
    public DbSet<OpeningSettlement> OpeningSettlements => Set<OpeningSettlement>();
    public DbSet<MasterImportBatch> MasterImportBatches => Set<MasterImportBatch>();
    public DbSet<MasterImportStagingRow> MasterImportStagingRows => Set<MasterImportStagingRow>();

    public async Task<string> GenerateSalesBillNumberAsync(int year, CancellationToken cancellationToken)
        => await Database.SqlQueryRaw<string>(
                "SELECT generate_sales_bill_number({0}) AS \"Value\"", year)
            .SingleAsync(cancellationToken);

    // Keep audit capture on the context's SaveChanges path so every application
    // write through IAccountingInventoryDbContext is captured regardless of EF
    // interceptor registration or sync/async handler usage.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        InvoiceSnapshotCapture.CaptureAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        CaptureAuditLogs();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await InvoiceSnapshotCapture.CaptureAsync(this, cancellationToken);
        CaptureAuditLogs();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void CaptureAuditLogs()
    {
        ChangeTracker.DetectChanges();
        var actor = currentUserProvider?.UserId;
        var now = DateTimeOffset.UtcNow;
        var auditLogs = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            var originalState = entry.State;
            var tableName = entry.Metadata.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName)) continue;

            var oldValues = originalState is EntityState.Modified or EntityState.Deleted
                ? Snapshot(entry, originalValues: true, includeAll: originalState == EntityState.Deleted)
                : null;

            switch (originalState)
            {
                case EntityState.Added:
                    entry.Property(entity => entity.CreatedAt).CurrentValue = now;
                    entry.Property(entity => entity.CreatedBy).CurrentValue = actor;
                    break;
                case EntityState.Modified:
                    entry.Property(entity => entity.ModifiedAt).CurrentValue = now;
                    entry.Property(entity => entity.ModifiedBy).CurrentValue = actor;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Property(entity => entity.IsDeleted).CurrentValue = true;
                    entry.Property(entity => entity.ModifiedAt).CurrentValue = now;
                    entry.Property(entity => entity.ModifiedBy).CurrentValue = actor;
                    break;
            }

            var action = originalState switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ when entry.Property(nameof(AuditableEntity.IsDeleted)).CurrentValue is true
                    && entry.Property(nameof(AuditableEntity.IsDeleted)).OriginalValue is false => "Delete",
                _ => "Update"
            };
            var newValues = Snapshot(entry, originalValues: false,
                includeAll: originalState is EntityState.Added or EntityState.Deleted);

            auditLogs.Add(AuditLog.Create(tableName,
                entry.Property(nameof(AuditableEntity.Id)).CurrentValue?.ToString() ?? string.Empty,
                action, oldValues, newValues, actor, now, SchemaName));
        }

        if (auditLogs.Count > 0) AuditLogs.AddRange(auditLogs);
    }

    private static string? Snapshot(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        bool originalValues, bool includeAll)
    {
        var values = entry.Properties
            .Where(property => includeAll || property.IsModified)
            .Where(property => !IsSensitive(property.Metadata.Name))
            .ToDictionary(property => property.Metadata.Name,
                property => originalValues ? property.OriginalValue : property.CurrentValue);
        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static bool IsSensitive(string propertyName)
        => propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema selection is deliberately connection-scoped (Npgsql Search Path),
        // not model-scoped.  EF migrations must contain unqualified table names so
        // the same migration can run in every tenant schema.
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new VendorConfiguration());
        modelBuilder.ApplyConfiguration(new FinanceVendorConfiguration());
        modelBuilder.ApplyConfiguration(new BrandConfiguration());
        modelBuilder.ApplyConfiguration(new VariantConfiguration());
        modelBuilder.ApplyConfiguration(new ColorConfiguration());
        modelBuilder.ApplyConfiguration(new ProductTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ProductModelConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new SalesProductConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new SalesPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new SalesReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerBillSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new PurchasePaymentConfiguration());
        modelBuilder.ApplyConfiguration(new TaxConfiguration());
        modelBuilder.ApplyConfiguration(new ChartAccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new JournalLineConfiguration());
        modelBuilder.ApplyConfiguration(new AccountingPeriodConfiguration());
        modelBuilder.ApplyConfiguration(new BankReconciliationConfiguration());
        modelBuilder.ApplyConfiguration(new BankStatementLineConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new AccountingApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new AccountingDimensionConfiguration());
        modelBuilder.ApplyConfiguration(new SupportingDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new FixedAssetConfiguration());
        modelBuilder.ApplyConfiguration(new AccountBudgetConfiguration());
        modelBuilder.ApplyConfiguration(new AccountingUserPermissionConfiguration());
        P0Configurations.Configure(modelBuilder);
        modelBuilder.Entity<JournalLine>().HasOne<AccountingDimension>().WithMany()
            .HasForeignKey(line => line.DimensionId).OnDelete(DeleteBehavior.Restrict);
        //modelBuilder.ApplyConfiguration(new RoleConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public Task ResetConnectionAsync(CancellationToken cancellationToken)
        => Database.CloseConnectionAsync();
}
