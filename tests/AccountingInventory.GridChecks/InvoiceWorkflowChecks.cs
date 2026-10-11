using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Customers;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Inventory;
using AccountingInventory.Application.Sales.Invoices;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;
using System.Text.Json;

internal static class InvoiceWorkflowChecks
{
    public static async Task RunAsync(AccountingInventoryDbContext db)
    {
        await CheckUpgradeAsync(db);
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); }
            catch (Exception e) when (e is ArgumentException or ConflictException or FluentValidation.ValidationException)
            { db.ChangeTracker.Clear(); count++; return; }
            throw new Exception(message);
        }
        var date = new DateOnly(2026, 2, 1);
        var customer = Customer.Create("Invoice customer", "8123456789", "Customer address", null);
        var owner = User.Create("invoice-owner", "9123456789", "owner@example.com"); owner.SetOwner(true); owner.AcceptInvitation();
        var vendor = Vendor.Create("Invoice supplier", "INV-V", "7123456789", "vendor@example.com", null, "Supplier address");
        var brand = Brand.Create("Invoice brand", null); var type = ProductType.Create("Phone", null);
        var model = ProductModel.Create(brand.Id, type.Id, "INV-M", "Model", null);
        var variant = Variant.Create("128GB", null); var color = Color.Create("Blue", null); var tax = Tax.Create(9, 9);
        var stock = Product.Create(vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id, "INV-IMEI", "INV-ALT", "INV-PURCHASE", 100, 0, 9, 9, 18, date);
        var settings = CustomerBillSettings.Create("Frozen seller", "Seller address", "9123456780", null, "27AABCU9603R1ZM", "TAX INVOICE", "Thank you", "A4", true, true, true, true, true, "27", "Maharashtra");
        db.AddRange(customer, owner, vendor, brand, type, model, variant, color, tax, stock, settings);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, default);
        LedgerPosting.Add(db, LedgerPosting.ForPurchase(stock, accounts)); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE SEQUENCE invoice_fixture_counter; CREATE FUNCTION generate_sales_bill_number(integer) RETURNS text LANGUAGE sql AS $$ SELECT 'INV-' || nextval('invoice_fixture_counter')::text $$");
        var line = new CreateSalesInvoiceLineCommandDto(InvoiceItemType.SerializedProduct, stock.Id, "Phone", "CLIENT-FAKE", null, 1, 200, TaxId: tax.Id, HsnSac: "8517");
        var fractional = new CreateSalesInvoiceLineCommandDto(InvoiceItemType.Service, null, "Service", null, null, 0.3333m, 10, HsnSac: "998599");
        var command = new CreateSalesInvoiceCommand(customer.Name, customer.Mobile, customer.Address, null, date, 30, null,
            [line, fractional, fractional], new(50, "Cash", date), PlaceOfSupplyStateCode: "27");
        await Reject(() => new CreateSalesInvoiceCommandHandler(db).Handle(command with { Lines = [line, line] }, default), "Duplicate serialized products must be rejected.");
        await Reject(() => new CreateSalesInvoiceCommandHandler(db).Handle(command with { Lines = [line with { Quantity = 2 }] }, default), "Serialized quantity must be one.");
        await Reject(() => new CreateSalesInvoiceCommandHandler(db).Handle(command with { SupplyType = GstSupplyType.InterState }, default), "Contradictory supply type must be rejected.");
        var details = await new CreateSalesInvoiceCommandHandler(db).Handle(command, default);
        var id = details.Invoice.Id;
        Check(details.Invoice.TotalAmount == 242.66m && details.Invoice.SubTotal == 206.66m, "Fractional lines must round consistently and post a balanced invoice.");
        Check(details.Lines.First().SerialNumber == stock.SerialNumber, "Inventory serials must be authoritative.");
        Check(await db.SalesInvoiceReceipts.CountAsync() == 1 && await db.SalesReceipts.CountAsync() == 0, "Invoice receipt must persist without a legacy sales-product FK.");
        Check((await new GetSalesInvoiceDetailsQueryHandler(db).Handle(new(id), default)).Payments.Single().Amount == 50, "Invoice details must return its receipts.");
        var historical = await new GetAgingReportHandler(db).Handle(new(date), default);
        Check(historical.TotalReceivables == 192.66m, "Aging must include the new invoice and initial receipt.");
        var payments = new AllocateCustomerPaymentCommandHandler(db);
        await Reject(() => payments.Handle(new(customer.Id, 10, "Cash", date, AutoAllocateFifo: false, SpecificAllocations: [new(id, 20)]), default), "Allocation must not exceed received money.");
        await Reject(() => payments.Handle(new(customer.Id, 10, "Cash", date, AutoAllocateFifo: false, SpecificAllocations: [new(id, 5), new(id, 5)]), default), "Duplicate allocation targets must fail.");
        await Reject(() => payments.Handle(new(customer.Id, 10, "Cash", date.AddDays(-1)), default), "Allocation must not precede invoice date.");
        await Reject(() => new RecordSalesInvoicePaymentCommandHandler(db).Handle(new(id, 1.001m, "Cash", date), default), "Sub-paise money must fail.");
        var result = await payments.Handle(new(customer.Id, 300, "Cash", date.AddDays(1)), default);
        Check(result.TotalAllocated == 192.66m && result.UnallocatedAdvance == 107.34m, "FIFO must settle invoice and capture excess as an advance.");
        var advance = await db.CustomerAdvances.SingleAsync();
        var service = await new CreateSalesInvoiceCommandHandler(db).Handle(command with {
            InvoiceDate = date.AddDays(2), InitialPayment = null,
            Lines = [new(InvoiceItemType.Service, null, "Service credit", null, null, 1, 40, HsnSac: "998599")] }, default);
        var advances = new CustomerAdvancesHandler(db);
        await advances.Handle(new ApplyCustomerAdvanceCommand(advance.Id, service.Invoice.Id, 40, date.AddDays(2)), default);
        Check(await db.SalesInvoiceReceipts.AnyAsync(r => r.AdvanceId == advance.Id && r.Amount == 40), "Advance application must persist its link.");
        Check(await db.JournalEntries.CountAsync(e => e.SourceType == "CustomerAdvance") == 1, "Applying an advance must not receive cash twice.");
        await advances.Handle(new RefundCustomerAdvanceCommand(advance.Id, 67.34m, date.AddDays(2), "Cash", null), default);
        Check((await advances.Handle(new GetCustomerAdvancesQuery(customer.Id), default)).Count == 0, "Fully used/refunded advance must have no remaining credit.");
        var actor = new Identity(owner.Id); var corrections = new InvoiceCorrectionHandler(db, actor);
        var note = await corrections.Handle(new ReturnInvoiceCommand("Sale", id, date.AddDays(3), "Whole invoice returned", "Restock"), default);
        await corrections.Handle(new RefundCorrectionCommand(note.Id, date.AddDays(3), 242.66m, "Cash", null), default);
        Check(!await db.Products.Where(p => p.Id == stock.Id).Select(p => p.IsSold).SingleAsync(), "Invoice return must restore inventory.");
        var controls = await new OpeningReconciliationHandler(db).Handle(new GetControlReconciliationQuery(date.AddDays(3)), default);
        Check(controls.ReceivableDifference == 0 && controls.PayableDifference == 0 && controls.StockDifference == 0, "Controls must reconcile after invoice, allocation, advance, return and both refund types.");
        var statement = await new GetPartyStatementHandler(db).Handle(new("Customer", customer.Id, date, date.AddDays(3)), default);
        Check(statement.ClosingBalance == 0 && statement.Transactions.Any(t => t.VoucherType == "Customer Refund") && statement.Transactions.Any(t => t.VoucherType == "Advance Refund"), "Customer statement must include all refunds and advances without duplicate credits.");
        var oldStatement = await new GetPartyStatementHandler(db).Handle(new("Customer", customer.Id, date, date), default);
        Check(oldStatement.Aging.TotalOutstanding == 192.66m && oldStatement.ClosingBalance == 192.66m, "Historical statement aging must ignore later payments and returns.");
        var vendorStatement = await new GetPartyStatementHandler(db).Handle(new("Vendor", vendor.Id, date, date.AddDays(3)), default);
        Check(vendorStatement.Aging.Current0To30 == 118 && vendorStatement.Aging.TotalOutstanding == 118, "Vendor aging must populate its buckets.");
        var movements = await new GetStockMovementsHandler(db).Handle(new(PageSize: 100), default);
        Check(movements.Items.Any(m => m.Reference == details.Invoice.BillNumber && m.MovementType == "Sale"), "Stock movement history must include new invoice sales.");
        var taxReport = await new GetTaxReportHandler(db).Handle(new(date, date.AddDays(3)), default);
        Check(taxReport.OutputTaxDifference == 0 && taxReport.OutputTax == 0, "Returned invoice tax must reconcile to its reversal.");
        Check(taxReport.Sales.All(r => r.TotalTax == 0), "Returns must reverse the original tax rate buckets.");
        var history = await new GetCustomerHistoryQueryHandler(db).Handle(new(customer.Id), default);
        Check(history.Sales.Count == 2 && history.Customer.SalesCount == 2, "Customer history must include new invoices.");
        Check(history.TotalReceived == 40m, "Net cash received must exclude advance applications and subtract refunds.");
        await Reject(() => new PostJournalHandler(db).Handle(new PostJournalCommand(date.AddDays(4), "Invalid control posting", "Manual", null,
            [new(accounts["1100"], 10, 0), new(accounts["4000"], 0, 10)]), default), "Ordinary journals must not bypass customer subledgers.");
        await Reject(() => new WriteOffInventoryHandler(db).Handle(new(stock.Id, date.AddDays(-1), "Early writeoff"), default), "Writeoff must not predate purchase.");
        await Reject(() => new WriteOffInventoryHandler(db).Handle(new(stock.Id, date.AddDays(1), "Before return"), default), "Writeoff must not predate stock restoration.");
        await Reject(() => new DeleteCustomerCommandHandler(db).Handle(new(customer.Id), default), "Customer with new invoice history cannot be deleted.");
        var snapshot = await db.InvoiceSnapshots.SingleAsync(s => s.SourceId == id);
        Check(JsonDocument.Parse(snapshot.DetailsJson).RootElement.GetProperty("seller").GetProperty("companyName").GetString() == "Frozen seller", "Snapshot must freeze seller identity.");
        // IGST invoice and full return must reverse the tax as well as the revenue.
        var inter = await new CreateSalesInvoiceCommandHandler(db).Handle(command with { Lines = [line with { ItemType = InvoiceItemType.Service, ProductId = null }],
            InitialPayment = null, InvoiceDate = date.AddDays(4), PlaceOfSupplyStateCode = "29" }, default);
        Check(inter.Invoice.IgstAmount == 36 && inter.Invoice.CgstAmount == 0, "Interstate invoice uses IGST.");
        var interNote = await corrections.Handle(new ReturnInvoiceCommand("Sale", inter.Invoice.Id, date.AddDays(5), "Interstate cancellation", "Restock"), default);
        Check(interNote.IgstAmount == 36, "Credit note must capture IGST reversal.");
        var interReport = await new GetTaxReportHandler(db).Handle(new(date.AddDays(4), date.AddDays(5)), default);
        Check(interReport.OutputTax == 0 && interReport.OutputTaxDifference == 0, "IGST return must reconcile.");
        Check(interReport.Sales.All(r => r.IgstAmount == 0), "IGST return must reverse the original IGST rate bucket.");
        await Reject(() => new RecordSalesInvoicePaymentCommandHandler(db).Handle(new(inter.Invoice.Id, 1, "Cash", date.AddDays(5)), default), "Cancelled invoices cannot receive payments.");
        // Downgrades must preserve used receipt and advance history.
        var tx = db.Database.CurrentTransaction!; await tx.CreateSavepointAsync("invoice_down_guard");
        var blocked = false;
        try {
            foreach (var sql in db.GetService<IMigrationsSqlGenerator>().Generate(new MandatoryInvoiceIntegrity().DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        } catch (Npgsql.PostgresException e) when (e.SqlState == "P0001") { blocked = true; await tx.RollbackToSavepointAsync("invoice_down_guard"); }
        Check(blocked && await db.SalesInvoiceReceipts.AnyAsync(), "Used invoice history must block downgrade.");
        await SkuWorkflowChecks.RunAsync(db,customer,vendor,tax,owner.Id,date.AddDays(10));
        Console.WriteLine($"PASS: {count} new invoice, advance, return, historical report and migration checks.");
    }
    private static async Task CheckUpgradeAsync(AccountingInventoryDbContext db)
    {
        if (!db.SchemaName.StartsWith("invoice_check_", StringComparison.Ordinal))
            throw new Exception("Invoice migration checks require a scratch schema.");
        var tx = db.Database.CurrentTransaction!;
        await tx.CreateSavepointAsync("invoice_upgrade");
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var sql in generator.Generate(new AccessorySkuInventory().DownOperations,db.Model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        foreach (var sql in generator.Generate(new AccessorySkuInventory().UpOperations,db.Model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        if(await db.StockSkus.AnyAsync()) throw new Exception("Fresh accessory upgrade must preserve an empty stock balance.");
        foreach (var sql in generator.Generate(new MandatoryInvoiceIntegrity().DownOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var customer = Customer.Create("Legacy advance", "8111111111", "Address", null);
        db.Customers.Add(customer);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, default);
        var entry = JournalEntry.Post(new(2026, 1, 1), "Legacy customer credit", "CustomerAdvance", customer.Id.ToString(),
            [(accounts["1000"], 125.50m, 0m, null), (accounts["1100"], 0m, 125.50m, null)]);
        LedgerPosting.Add(db, entry);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        foreach (var sql in generator.Generate(new MandatoryInvoiceIntegrity().UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var advance = await db.CustomerAdvances.SingleAsync();
        var journal = await db.JournalEntries.SingleAsync();
        if (advance.Id != entry.Id || advance.CustomerId != customer.Id || advance.RemainingAmount != 125.50m
            || advance.PaymentMode != "Cash" || journal.SourceId != advance.Id.ToString())
            throw new Exception("Invoice upgrade must preserve and link legacy customer advances.");
        await tx.RollbackToSavepointAsync("invoice_upgrade");
        db.ChangeTracker.Clear();
        Console.WriteLine("PASS: additive invoice migration and legacy advance backfill.");
    }
    private sealed class Identity(Guid id) : IRequestIdentity { public Guid? UserId => id; }
}
