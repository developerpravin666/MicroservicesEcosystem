using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Purchases.Products;
using AccountingInventory.Application.Purchases.Accounting;
using AccountingInventory.Application.Sales.Accounting;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

internal static class BusinessIntegrityChecks
{
    public static async Task RunAsync(AccountingInventoryDbContext db)
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }
        async Task Reject<T>(Func<Task> action, string message) where T : Exception
        {
            try { await action(); }
            catch (T) { checks++; return; }
            throw new Exception(message);
        }
        var stock = await db.Products.FirstAsync(product => !product.IsSold);
        var reference = new PurchaseReference(stock.VendorId, stock.BrandId, stock.ProductTypeId,
            stock.ProductModelId, stock.VariantId, stock.ColorId, "NEW-BILL", stock.PurchaseDate, 0);
        await PurchaseIntegrity.ValidateAsync(db, [reference], default);
        checks++;
        await Reject<ConflictException>(() => PurchaseIntegrity.ValidateAsync(db,
            [reference with { BrandId = Guid.NewGuid() }], default), "Foreign-tenant references must be rejected");
        await Reject<ConflictException>(() => AccountingInventory.Application.Common.MasterReferenceIntegrity.EnsureDeletableAsync(
            db, "Vendor", stock.VendorId, default), "Referenced vendors must not disappear from invoices");
        await Reject<ConflictException>(() => AccountingInventory.Application.Common.MasterReferenceIntegrity.EnsureDeletableAsync(
            db, "ProductModel", stock.ProductModelId, default), "Referenced models must not disappear from inventory");
        var anotherBrand = await db.Brands.FirstAsync(brand => brand.Id != stock.BrandId);
        await Reject<ConflictException>(() => PurchaseIntegrity.ValidateAsync(db,
            [reference with { BrandId = anotherBrand.Id }], default), "Mismatched brand/model must be rejected");
        await Reject<ConflictException>(() => PurchaseIntegrity.ValidateAsync(db,
            [reference, reference with { PaymentTermsDays = 30 }], default), "Mixed invoice terms must be rejected");
        await Reject<ConflictException>(() => PurchaseIntegrity.ValidateAsync(db,
            [reference with { BillNumber = stock.BillNumber, PurchaseDate = stock.PurchaseDate.AddDays(1) }], default),
            "Existing invoice dates cannot diverge");
        var paidProduct = await db.Products.FirstAsync(product => product.BillNumber == "B1");
        await Reject<ConflictException>(() => PurchaseIntegrity.ValidateAsync(db,
            [new(paidProduct.VendorId, paidProduct.BrandId, paidProduct.ProductTypeId, paidProduct.ProductModelId,
                paidProduct.VariantId, paidProduct.ColorId, " b1 ", paidProduct.PurchaseDate, 0)], default),
            "Paid invoices cannot accept new units");
        var input = new AccountingInventory.Application.Sales.Products.Commands.CreateSalesProduct.CreateSalesProductCommand(
            stock.Id.ToString(), "Customer", "9000000000", "Address", null, stock.PurchaseDate, 100, 100, 0);
        var invoiceValidator = new AccountingInventory.Application.Sales.Products.Commands.CreateSalesProduct.CreateSalesProductCommandValidator();
        Check(invoiceValidator.Validate(input).IsValid, "Ordinary invoice amounts remain valid");
        Check(!invoiceValidator.Validate(input with { SellingPrice = 100.005m }).IsValid, "Sub-cent prices must be rejected before ledger posting");
        Check(!invoiceValidator.Validate(input with { Discount = 0.004m }).IsValid, "Sub-cent discounts must be rejected before ledger posting");
        var sale = await db.SalesProducts.FirstAsync();
        await Reject<ConflictException>(() => new RecordSalesReceiptHandler(db).Handle(
            new(sale.Id, 1, "Cash", sale.SaleDate.AddDays(-1), null, null), default), "Receipt cannot precede invoice");
        Check(!new RecordSalesReceiptValidator().Validate(new RecordSalesReceiptCommand(sale.Id, 0.001m, "Cash", sale.SaleDate, null, null)).IsValid,
            "Sub-cent receipts must be rejected before posting");
        Check(!new RecordSalesReceiptValidator().Validate(new RecordSalesReceiptCommand(sale.Id, 0m, "Cash", sale.SaleDate, null, null)).IsValid,
            "Zero receipts must be rejected");
        Check(new RecordSalesReceiptValidator().Validate(new RecordSalesReceiptCommand(sale.Id, 1.25m, "Cash", sale.SaleDate, null, null)).IsValid,
            "Normal receipts remain valid");
        await Reject<ConflictException>(() => new RecordPurchasePaymentCommandHandler(db).Handle(
            new(stock.VendorId, stock.BillNumber!, 1, "Cash", stock.PurchaseDate.AddDays(-1), null, null), default),
            "Payment cannot precede purchase invoice");

        var owner = User.Create("owner", "9000000000", "owner@example.com");
        owner.CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        owner.Activate(); owner.SetOwner(true); owner.SetPasswordHash("test-only");
        var staff = User.Create("staff", "9000000001", "staff@example.com");
        staff.CreatedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        staff.Activate(); staff.SetPasswordHash("test-only");
        db.Users.Add(owner); await db.SaveChangesAsync();
        db.Users.Add(staff); await db.SaveChangesAsync();
        await AccountingPermissionGate.EnsureAsync(db, owner.Id, "purchases.manage", default); checks++;
        await Reject<ForbiddenException>(() => AccountingPermissionGate.EnsureAsync(db, staff.Id, "purchases.manage", default),
            "Staff writes must require a grant");
        await Reject<ForbiddenException>(() => AccountingPermissionGate.EnsureAsync(db, null, "purchases.manage", default),
            "Anonymous writes must be rejected");
        db.AccountingUserPermissions.Add(AccountingUserPermission.Grant(staff.Id, "purchases.manage", owner.Id));
        await db.SaveChangesAsync();
        await AccountingPermissionGate.EnsureAsync(db, staff.Id, "purchases.manage", default); checks++;
        await Reject<ForbiddenException>(() => AccountingPermissionGate.EnsureAsync(db, staff.Id, "sales.manage", default),
            "Purchase grants must not permit sales writes");

        await new InitializeChartOfAccountsHandler(db).Handle(new(), default);
        var accounts = await db.ChartAccounts.ToDictionaryAsync(account => account.Code);
        var date = new DateOnly(2026, 1, 10);
        var revenue = JournalEntry.Post(date, "Revenue test", "Sale", "test-sale",
            [(accounts["1000"].Id, 100m, 0m, (string?)null), (accounts["4000"].Id, 0m, 100m, (string?)null)]);
        db.JournalEntries.Add(revenue); db.JournalLines.AddRange(revenue.Lines); await db.SaveChangesAsync();
        await Reject<ConflictException>(() => new ReverseJournalHandler(db).Handle(new(revenue.Id, date, null), default),
            "System source journals cannot be manually reversed");
        await Reject<ConflictException>(() => new PostJournalHandler(db).Handle(new(date, "Fabricated source", "Sale", "fake",
            [new(accounts["1000"].Id, 10, 0), new(accounts["4000"].Id, 0, 10)]), default),
            "Manual journals cannot impersonate a sale");
        var before = await new GetFinancialStatementsHandler(db).Handle(new(date, date), default);
        Check(before.CurrentEarnings == 100 && before.BalanceDifference == 0, "Balance sheet must balance before close");
        var period = AccountingPeriod.Create("Test year", new(2026, 1, 1), new(2026, 12, 31));
        db.AccountingPeriods.Add(period); await db.SaveChangesAsync();
        await new CloseAccountingPeriodHandler(db).Handle(new(period.Id), default);
        var after = await new GetFinancialStatementsHandler(db).Handle(new(new(2026, 1, 1), new(2026, 12, 31)), default);
        Check(after.CurrentEarnings == 0 && after.TotalEquity == 100 && after.BalanceDifference == 0,
            "Closing earnings must not be counted twice");
        Check(after.NetIncome == 100, "Income statement retains operating income after close");
        await Reject<ConflictException>(() => new CloseAccountingPeriodHandler(db).Handle(new(period.Id), default),
            "Periods cannot be closed twice");
        await Reject<ConflictException>(() => LedgerPosting.EnsurePeriodOpenAsync(db, date, default), "Closed periods reject postings");

        // Use the next open year for reversal tests.
        var nextDate = new DateOnly(2027, 1, 10);
        var dimension = AccountingDimension.Create("SHOP", "Shop", "COST_CENTER");
        db.AccountingDimensions.Add(dimension);
        var manual = JournalEntry.Post(nextDate, "Manual test", "Manual", "manual-test",
            [(accounts["1000"].Id, 25m, 0m, (string?)new string('M', 250)), (accounts["3000"].Id, 0m, 25m, (string?)null)]);
        foreach (var line in manual.Lines) line.AssignDimension(dimension.Id);
        db.JournalEntries.Add(manual); db.JournalLines.AddRange(manual.Lines); await db.SaveChangesAsync();
        await Reject<ConflictException>(() => new ReverseJournalHandler(db).Handle(new(manual.Id, nextDate.AddDays(-1)), default),
            "Reversal cannot precede original");
        var reversed = await new ReverseJournalHandler(db).Handle(new(manual.Id, nextDate.AddDays(1)), default);
        Check(reversed.Lines.Any(line => line.Memo?.Length == 250), "Reversals preserve maximum-length memos without overflowing the column");
        Check(reversed.Lines.All(line => line.DimensionId == dimension.Id), "Reversals preserve dimensions");
        await Reject<ConflictException>(() => new ReverseJournalHandler(db).Handle(new(manual.Id, nextDate.AddDays(2)), default),
            "Duplicate reversals must be rejected");
        var originalBill = stock.BillNumber!;
        var originalTotal = await db.Products.Where(product => product.VendorId == stock.VendorId
            && product.BillNumber != null && product.BillNumber.ToLower() == originalBill.ToLower()).SumAsync(product => product.TotalAmount);
        var caseVariant = Product.Create(stock.VendorId, stock.BrandId, stock.ProductTypeId, stock.ProductModelId,
            stock.VariantId, stock.ColorId, "CASE-UNIT", "CASE-ALT", originalBill.ToLowerInvariant(),
            10, 0, 0, 0, 0, stock.PurchaseDate);
        db.Products.Add(caseVariant); await db.SaveChangesAsync();
        var grouped = await new GetPurchaseBillsQueryHandler(db).Handle(new(Search: originalBill), default);
        Check(grouped.TotalCount == 1 && grouped.Items.Single().TotalAmount == originalTotal + 10,
            "Case variants of a vendor bill form one invoice with the full total");
        var details = await new GetPurchaseBillDetailsQueryHandler(db).Handle(new(stock.VendorId, originalBill), default);
        Check(details.Bill.TotalAmount == originalTotal + 10, "Invoice details agree with case-insensitive grouping");
        var swapped = User.Create("legacy", "legacy@example.com", "9876543210");
        swapped.SetPasswordHash("test-only"); db.Users.Add(swapped); await db.SaveChangesAsync();
        var migration = new AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb.RepairUserContacts();
        foreach (var sql in migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(sql.Sql);
        var repaired = await db.Users.AsNoTracking().SingleAsync(user => user.Id == swapped.Id);
        Check(repaired.Email == "legacy@example.com" && repaired.Mobile == "9876543210", "Legacy contact migration repairs swapped values");
        var untouched = await db.Users.AsNoTracking().SingleAsync(user => user.Id == owner.Id);
        Check(untouched.Email == "owner@example.com" && untouched.Mobile == "9000000000", "Contact migration preserves correct pairs");
        Console.WriteLine($"PASS: {checks} accounting and inventory integrity checks.");
    }
}
