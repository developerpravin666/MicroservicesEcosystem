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
    public DbSet<Godown> Godowns => Set<Godown>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockTransferJournal> StockTransferJournals => Set<StockTransferJournal>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<EWayBill> EWayBills => Set<EWayBill>();
    public DbSet<EInvoice> EInvoices => Set<EInvoice>();
    public DbSet<RcmSelfInvoice> RcmSelfInvoices => Set<RcmSelfInvoice>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<HrMasterItem> HrMasterItems => Set<HrMasterItem>();

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
        modelBuilder.ApplyConfiguration(new GodownConfiguration());
        modelBuilder.ApplyConfiguration(new StockBalanceConfiguration());
        modelBuilder.ApplyConfiguration(new StockTransferJournalConfiguration());
        modelBuilder.ApplyConfiguration(new StockTransferLineConfiguration());
        modelBuilder.ApplyConfiguration(new StockAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new UnitOfMeasureConfiguration());
        modelBuilder.ApplyConfiguration(new EWayBillConfiguration());
        modelBuilder.ApplyConfiguration(new EInvoiceConfiguration());
        P0Configurations.Configure(modelBuilder);

        modelBuilder.Entity<Employee>(b =>
        {
            b.ToTable("hr_employees");
            b.HasKey(x => x.Id);
            b.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            b.Property(x => x.Designation).HasMaxLength(150).IsRequired();
            b.Property(x => x.Department).HasMaxLength(100);
            b.Property(x => x.MonthlyBaseSalary).HasPrecision(18, 2);
            b.HasIndex(x => x.EmployeeCode).IsUnique();
        });

        modelBuilder.Entity<Department>(b =>
        {
            b.ToTable("hr_departments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<LeaveType>(b =>
        {
            b.ToTable("hr_leave_types");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<LeaveBalance>(b =>
        {
            b.ToTable("hr_leave_balances");
            b.HasKey(x => x.Id);
            b.Property(x => x.TotalAllocated).HasPrecision(8, 2);
            b.Property(x => x.UsedDays).HasPrecision(8, 2);
            b.Property(x => x.PendingApprovalDays).HasPrecision(8, 2);
            b.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        });

        modelBuilder.Entity<LeaveRequest>(b =>
        {
            b.ToTable("hr_leave_requests");
            b.HasKey(x => x.Id);
            b.Property(x => x.TotalDays).HasPrecision(8, 2);
            b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            b.HasIndex(x => x.EmployeeId);
            b.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<SalaryStructure>(b =>
        {
            b.ToTable("hr_salary_structures");
            b.HasKey(x => x.Id);
            b.Property(x => x.BasicSalary).HasPrecision(18, 2);
            b.Property(x => x.HouseRentAllowance).HasPrecision(18, 2);
            b.Property(x => x.Hra).HasPrecision(18, 2);
            b.Property(x => x.DearnessAllowance).HasPrecision(18, 2);
            b.Property(x => x.ConveyanceAllowance).HasPrecision(18, 2);
            b.Property(x => x.SpecialAllowance).HasPrecision(18, 2);
            b.Property(x => x.MedicalAllowance).HasPrecision(18, 2);
            b.Property(x => x.PerformanceBonus).HasPrecision(18, 2);
            b.Property(x => x.ProvidentFund).HasPrecision(18, 2);
            b.Property(x => x.Esi).HasPrecision(18, 2);
            b.Property(x => x.ProfessionalTax).HasPrecision(18, 2);
            b.Property(x => x.TdsTax).HasPrecision(18, 2);
            b.Property(x => x.OtherDeductions).HasPrecision(18, 2);
            b.HasIndex(x => x.EmployeeId).IsUnique();
        });

        modelBuilder.Entity<PayrollRun>(b =>
        {
            b.ToTable("hr_payroll_runs");
            b.HasKey(x => x.Id);
            b.Property(x => x.TotalGrossAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalDeductions).HasPrecision(18, 2);
            b.Property(x => x.TotalNetAmount).HasPrecision(18, 2);
            b.HasIndex(x => new { x.Month, x.Year });
        });

        modelBuilder.Entity<Payslip>(b =>
        {
            b.ToTable("hr_payslips");
            b.HasKey(x => x.Id);
            b.Property(x => x.BasicSalary).HasPrecision(18, 2);
            b.Property(x => x.HouseRentAllowance).HasPrecision(18, 2);
            b.Property(x => x.Hra).HasPrecision(18, 2);
            b.Property(x => x.Allowances).HasPrecision(18, 2);
            b.Property(x => x.Bonus).HasPrecision(18, 2);
            b.Property(x => x.GrossEarnings).HasPrecision(18, 2);
            b.Property(x => x.ProvidentFund).HasPrecision(18, 2);
            b.Property(x => x.PfDeduction).HasPrecision(18, 2);
            b.Property(x => x.EmployeeStateInsurance).HasPrecision(18, 2);
            b.Property(x => x.EsiDeduction).HasPrecision(18, 2);
            b.Property(x => x.ProfessionalTax).HasPrecision(18, 2);
            b.Property(x => x.TaxDeductedAtSource).HasPrecision(18, 2);
            b.Property(x => x.TdsDeduction).HasPrecision(18, 2);
            b.Property(x => x.LossOfPayDeduction).HasPrecision(18, 2);
            b.Property(x => x.LopDeduction).HasPrecision(18, 2);
            b.Property(x => x.OtherDeductions).HasPrecision(18, 2);
            b.Property(x => x.TotalDeductions).HasPrecision(18, 2);
            b.Property(x => x.NetSalary).HasPrecision(18, 2);
            b.Property(x => x.TotalWorkingDays);
            b.Property(x => x.WorkingDays);
            b.Property(x => x.DepartmentName).HasMaxLength(150);
            b.Property(x => x.Department).HasMaxLength(150);
            b.HasIndex(x => x.PayrollRunId);
            b.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<HrMasterItem>(b =>
        {
            b.ToTable("hr_master_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.HasIndex(x => new { x.Category, x.Code }).IsUnique();
        });
        modelBuilder.Entity<JournalLine>().HasOne<AccountingDimension>().WithMany()
            .HasForeignKey(line => line.DimensionId).OnDelete(DeleteBehavior.Restrict);
        //modelBuilder.ApplyConfiguration(new RoleConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public async Task ResetConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Database.CloseConnectionAsync();
        var connectionString = Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is not configured.");
        // Admin handlers resolve their target tenant after this context is created.
        // Reopening must use that tenant, rather than the original admin search path.
        Database.SetConnectionString(TenantSchemaMigrator.CreateTenantConnectionString(
            connectionString, tenantProvider.SchemaName));
    }
}
