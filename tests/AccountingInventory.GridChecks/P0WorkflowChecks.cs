using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Purchases.Accounting;
using AccountingInventory.Application.Sales.Accounting;
using AccountingInventory.Application.Sales.Products.Commands.CreateSalesProduct;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

internal static class P0WorkflowChecks
{
    public static async Task RunAsync(AccountingInventoryDbContext db)
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        async Task Reject<T>(Func<Task> action, string message) where T : Exception
        { try { await action(); } catch (T) { checks++; return; } throw new Exception(message); }
        var owner = User.Create("owner", "9000000000", "owner@example.com"); owner.SetOwner(true); owner.AcceptInvitation(); owner.SetPasswordHash("fixture");
        var customer = Customer.Create("Original customer", "8000000000", "Original address", "customer@example.com");
        var vendor = Vendor.Create("Original vendor", "V1", "7000000000", "vendor@example.com", null, "Original supplier address");
        var brand = Brand.Create("Brand", "Brand"); var type = ProductType.Create("Phone", "Phone");
        var model = ProductModel.Create(brand.Id, type.Id, "M", "Model", "Model");
        var variant = Variant.Create("Variant", "Variant"); var color = Color.Create("Blue", "Blue");
        db.AddRange(owner, customer, vendor, brand, type, model, variant, color); await db.SaveChangesAsync();
        var ids = await LedgerPosting.EnsureSystemAccountsAsync(db, default); await db.SaveChangesAsync();
        var cutoff = new DateOnly(2026, 1, 1);
        Product Stock(string serial, DateOnly date, decimal tax = 0) => Product.Create(vendor.Id, brand.Id, type.Id,
            model.Id, variant.Id, color.Id, serial, serial + "-ALT", serial + "-BILL", 100, 0, tax / 2, tax / 2, tax, date);
        var openingStock = Stock("OPENING", cutoff.AddDays(-1)); openingStock.StageOpeningStock(); db.Products.Add(openingStock); await db.SaveChangesAsync();
        Check(!openingStock.IsActive, "Staged opening stock must not be saleable");
        var openingCommand = new ImportOpeningBalancesCommand(cutoff,
            [new(ids["1100"], 200, 0), new(ids["2000"], 0, 150), new(ids["1200"], 100, 0)],
            [new("Customer", customer.Id, cutoff, "OLD-SALE", 200), new("Vendor", vendor.Id, cutoff, "OLD-PURCHASE", 150)], [openingStock.Id]);
        await Reject<ConflictException>(() => new ImportOpeningBalancesHandler(db).Handle(openingCommand with { Parties = [] }, default), "GL without matching opening subledgers must be rejected");
        await new ImportOpeningBalancesHandler(db).Handle(openingCommand, default);
        Check(openingStock.IsActive, "Reconciled stock must activate in the opening transaction");
        await Reject<ConflictException>(() => new ImportOpeningBalancesHandler(db).Handle(openingCommand with { CutoverDate = cutoff.AddDays(1) }, default), "Opening import must be one-time across all dates");
        var openingHandler = new OpeningReconciliationHandler(db);
        var openingItems = await openingHandler.Handle(new GetOpeningItemsQuery(), default);
        await Reject<ConflictException>(() => openingHandler.Handle(new SettleOpeningItemCommand(openingItems.Single(x => x.Kind == "Customer").Id, cutoff, 1, "Cash"), default), "Opening settlements must follow cutover");
        await Reject<ConflictException>(() => LedgerPosting.EnsurePeriodOpenAsync(db, cutoff, default), "Operational journals cannot be posted at cutover");
        await Reject<ConflictException>(() => new RecordPurchasePaymentCommandHandler(db).Handle(new(vendor.Id, openingStock.BillNumber!, 1, "Cash", cutoff.AddDays(1), null, null), default), "Opening stock cannot create a new purchase payable");
        await openingHandler.Handle(new SettleOpeningItemCommand(openingItems.Single(x => x.Kind == "Customer").Id, cutoff.AddDays(1), 50, "Cash"), default);
        await openingHandler.Handle(new SettleOpeningItemCommand(openingItems.Single(x => x.Kind == "Vendor").Id, cutoff.AddDays(1), 25, "Bank"), default);
        var normalStock = Stock("NORMAL", cutoff.AddDays(2), 18); db.Products.Add(normalStock);
        LedgerPosting.Add(db, LedgerPosting.ForPurchase(normalStock, ids)); await db.SaveChangesAsync();
        await new RecordPurchasePaymentCommandHandler(db).Handle(new(vendor.Id, normalStock.BillNumber!, 118, "Cash", cutoff.AddDays(2), null, null), default);
        var sale = SalesProduct.Create("SALE-1", openingStock.Id.ToString(), customer.Id, cutoff.AddDays(3), 100, 200, 0, null, 9, 9);
        openingStock.MarkSold(); db.SalesProducts.Add(sale); LedgerPosting.Add(db, LedgerPosting.ForSale(sale, openingStock, ids)); await db.SaveChangesAsync();
        await new RecordSalesReceiptHandler(db).Handle(new(sale.Id, 236, "Cash", cutoff.AddDays(3), null, null), default);
        db.Entry(customer).Property(x => x.Name).CurrentValue = "Changed customer";
        db.Entry(vendor).Property(x => x.Name).CurrentValue = "Changed vendor"; await db.SaveChangesAsync();
        var invoice = await new GetSalesBillDetailsQueryHandler(db).Handle(new(sale.Id), default);
        Check(invoice.Bill.CustomerName == "Original customer", "Printed invoices must keep the captured customer name");
        Check(await db.InvoiceSnapshots.AnyAsync(x => x.SourceId == normalStock.Id && x.PartyName == "Original vendor"), "Purchase supplier details must remain frozen");
        var actor = new FixtureIdentity(owner.Id); var returns = new InvoiceCorrectionHandler(db, actor);
        var note = await returns.Handle(new ReturnInvoiceCommand("Sale", sale.Id, cutoff.AddDays(4), "Customer returned unit", "Restock"), default);
        Check(sale.IsReturned && !openingStock.IsSold && openingStock.IsActive, "Customer return must restore saleable stock and preserve the sale");
        await Reject<ConflictException>(() => returns.Handle(new ReturnInvoiceCommand("Sale", sale.Id, cutoff.AddDays(4), "Duplicate", "Restock"), default), "Duplicate returns must fail");
        await Reject<ConflictException>(() => returns.Handle(new RefundCorrectionCommand(note.Id, cutoff.AddDays(4), 237, "Cash", null), default), "Refunds must not exceed paid credit");
        await returns.Handle(new RefundCorrectionCommand(note.Id, cutoff.AddDays(4), 236, "Cash", "REFUND-1"), default);
        await Reject<ConflictException>(() => new RecordSalesReceiptHandler(db).Handle(new(sale.Id, 1, "Cash", cutoff.AddDays(5), null, null), default), "Returned invoices cannot receive new receipts");
        var supplierNote = await returns.Handle(new ReturnInvoiceCommand("Purchase", normalStock.Id, cutoff.AddDays(5), "Return to supplier", "Supplier"), default);
        await returns.Handle(new RefundCorrectionCommand(supplierNote.Id, cutoff.AddDays(5), 118, "Bank", "REFUND-2"), default);
        Check(!normalStock.IsActive, "Supplier return must remove available stock");
        var report = await openingHandler.Handle(new GetControlReconciliationQuery(cutoff.AddDays(5)), default);
        Check(report.ReceivableSubledger == 150 && report.PayableSubledger == 125 && report.StockSubledger == 100,
            "Outstanding subledgers must include opening settlements and return refunds");
        Check(report.ReceivableDifference == 0 && report.PayableDifference == 0 && report.StockDifference == 0,
            "Subledger controls must reconcile after purchase/payment, sale/receipt, both returns and refunds");
        var tax = await new GetTaxReportHandler(db).Handle(new(cutoff.AddDays(2), cutoff.AddDays(5)), default);
        Check(tax.OutputTax == 0 && tax.InputTaxCredit == 0 && tax.OutputTaxDifference == 0 && tax.InputTaxCreditDifference == 0,
            "Tax reports and tax GL must include credit/debit notes");
        var resale = SalesProduct.Create("SALE-2", openingStock.Id.ToString(), customer.Id, cutoff.AddDays(6), 100, 200, 0, null, 9, 9);
        openingStock.MarkSold(); db.SalesProducts.Add(resale); LedgerPosting.Add(db, LedgerPosting.ForSale(resale, openingStock, ids)); await db.SaveChangesAsync();
        await returns.Handle(new ReturnInvoiceCommand("Sale", resale.Id, cutoff.AddDays(7), "Damaged returned unit", "WriteOff"), default);
        var writeOffReport = await openingHandler.Handle(new GetControlReconciliationQuery(cutoff.AddDays(7)), default);
        Check(!openingStock.IsActive && !openingStock.IsSold && writeOffReport.StockSubledger == 0 && writeOffReport.StockDifference == 0,
            "Damaged returns must write off the captured inventory cost and reconcile stock");
        var badJournal = await db.JournalLines.GroupBy(x => x.JournalEntryId).AnyAsync(g => g.Sum(x => x.Debit) != g.Sum(x => x.Credit));
        Check(!badJournal, "Every workflow journal must balance");
        var email = new FixtureEmail(); var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Frontend:BaseUrl"] = "https://example.test" }).Build();
        var admin = new StaffAdministrationHandler(db, actor, new FixtureTenant(), email, configuration, new PasswordHasher<User>());
        var staffId = await admin.Handle(new InviteStaffCommand("staff", "staff@example.com", "9000000001"), default);
        var staff = await db.Users.SingleAsync(x => x.Id == staffId); Check(!staff.IsActive && !staff.EmailVerified, "Invited staff cannot log in before verification");
        var token = Regex.Match(email.Body, "token=([A-F0-9]{64})").Groups[1].Value;
        Check(token.Length == 64 && staff.InvitationHash != token, "Invitation must contain an opaque token stored only as a hash");
        await admin.Handle(new AcceptStaffInvitationCommand(token, "StrongPassword123"), default);
        Check(staff.IsActive && staff.EmailVerified && staff.InvitationHash is null, "Activation must verify and consume the invitation");
        await Reject<ForbiddenException>(() => admin.Handle(new AcceptStaffInvitationCommand(token, "StrongPassword123"), default), "Invitations cannot be replayed");
        await Reject<ForbiddenException>(() => AccountingPermissionGate.EnsureAsync(db, staff.Id, "sales.manage", default), "New staff must not inherit owner access");
        await admin.Handle(new TransferOwnerCommand(staff.Id), default); Check(!owner.IsOwner && staff.IsOwner, "Ownership must transfer atomically to a verified user");
        await Reject<ForbiddenException>(() => AccountingPermissionGate.EnsureAsync(db, owner.Id, "sales.manage", default), "Earlier user ordering must not restore transferred ownership");
        Check(await db.AuditLogs.AnyAsync(x => x.TableName == "invoice_corrections"), "Financial corrections must have audit records");
        Console.WriteLine($"PASS: {checks} P0 accounting workflow checks");
    }
    private sealed class FixtureIdentity(Guid id) : IRequestIdentity { public Guid? UserId => id; }
    private sealed class FixtureTenant : ITenantContext { public Guid? TenantId => Guid.Parse("11111111-1111-1111-1111-111111111111"); public string? SchemaName => "fixture"; }
    private sealed class FixtureEmail : IEmailSender { public string Body = ""; public Task SendAsync(string recipient,string subject,string body,CancellationToken ct) { Body=body;return Task.CompletedTask; } public Task<bool> SendTestAsync(string recipient, CancellationToken ct) => Task.FromResult(false); }
}
