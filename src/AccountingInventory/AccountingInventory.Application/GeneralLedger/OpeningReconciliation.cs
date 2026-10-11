using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record OpeningItemSummary(Guid Id, string Kind, Guid PartyId, string PartyName, string Reference,
    DateOnly CutoverDate, DateOnly DueDate, decimal Amount, decimal Settled, decimal Balance);
public sealed record GetOpeningItemsQuery() : IRequest<IReadOnlyList<OpeningItemSummary>>;
public sealed record SettleOpeningItemCommand(Guid Id, DateOnly PaymentDate, decimal Amount, string PaymentMode) : IRequest<Guid>;
public sealed record GetControlReconciliationQuery(DateOnly? AsOf = null) : IRequest<ControlReconciliationSummary>;
public sealed record ControlReconciliationSummary(DateOnly AsOf, decimal ReceivableSubledger, decimal ReceivableLedger,
    decimal PayableSubledger, decimal PayableLedger, decimal StockSubledger, decimal StockLedger,
    decimal ReceivableDifference, decimal PayableDifference, decimal StockDifference);

public sealed class OpeningReconciliationHandler(IAccountingInventoryDbContext db) :
    IRequestHandler<GetOpeningItemsQuery, IReadOnlyList<OpeningItemSummary>>, IRequestHandler<SettleOpeningItemCommand, Guid>,
    IRequestHandler<GetControlReconciliationQuery, ControlReconciliationSummary>
{
    public async Task<IReadOnlyList<OpeningItemSummary>> Handle(GetOpeningItemsQuery q, CancellationToken ct)
    {
        var items = await db.OpeningSubledgerBalances.AsNoTracking().OrderBy(x => x.DueDate).ToArrayAsync(ct);
        var result = new List<OpeningItemSummary>();
        foreach (var item in items)
        {
            var settled = await db.OpeningSettlements.Where(x => x.OpeningBalanceId == item.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var name = item.Kind == "Customer" ? await db.Customers.Where(x => x.Id == item.PartyId).Select(x => x.Name).FirstOrDefaultAsync(ct)
                : await db.Vendors.Where(x => x.Id == item.PartyId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            result.Add(new(item.Id, item.Kind, item.PartyId, name ?? "", item.Reference, item.CutoverDate, item.DueDate, item.Amount, settled, item.Amount - settled));
        }
        return result;
    }
    public async Task<Guid> Handle(SettleOpeningItemCommand q, CancellationToken ct)
    {
        var item = await db.OpeningSubledgerBalances.SingleOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Opening item was not found.");
        if (q.PaymentDate <= item.CutoverDate) throw new ConflictException("Settlement must follow the cutover date.");
        var settled = await db.OpeningSettlements.Where(x => x.OpeningBalanceId == q.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var payment = OpeningSettlement.Create(q.Id, q.PaymentDate, q.Amount, q.PaymentMode);
        if (q.Amount > item.Amount - settled) throw new ConflictException("Settlement exceeds the opening balance remaining.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.PaymentDate, ct);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);
        var cash = accounts[q.PaymentMode == "Cash" ? "1000" : "1010"];
        var control = accounts[item.Kind == "Customer" ? "1100" : "2000"];
        var lines = item.Kind == "Customer" ? new[] { (cash, q.Amount, 0m, (string?)item.Reference), (control, 0m, q.Amount, (string?)item.Reference) }
            : new[] { (control, q.Amount, 0m, (string?)item.Reference), (cash, 0m, q.Amount, (string?)item.Reference) };
        db.OpeningSettlements.Add(payment);
        LedgerPosting.Add(db, JournalEntry.Post(q.PaymentDate, "Opening balance settlement", "OpeningSettlement", payment.Id.ToString(), lines));
        await db.SaveChangesAsync(ct); return payment.Id;
    }
    public async Task<ControlReconciliationSummary> Handle(GetControlReconciliationQuery q, CancellationToken ct)
    {
        var date = q.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var sales = await db.SalesProducts.Where(x => x.SaleDate <= date).ToArrayAsync(ct);
        var receipts = await db.SalesReceipts.Where(x => x.PaymentDate <= date).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var invoices = await db.SalesInvoices.Include(i => i.Lines).Where(i => i.InvoiceDate <= date).ToArrayAsync(ct);
        receipts += await db.SalesInvoiceReceipts.Where(r => r.PaymentDate <= date && r.AdvanceId == null).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var advanceAmount = await db.CustomerAdvances.Where(a => a.PaymentDate <= date).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        var advanceRefunds = await db.CustomerAdvanceRefunds.Where(a => a.PaymentDate <= date).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        var purchases = await db.Products.Where(x => x.PurchaseDate <= date).ToArrayAsync(ct);
        var payments = await db.PurchasePayments.Where(x => x.PaymentDate <= date).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var notes = await db.InvoiceCorrections.Where(x => x.NoteDate <= date).ToArrayAsync(ct);
        var refunds = await (from r in db.CorrectionRefunds join n in db.InvoiceCorrections on r.CorrectionId equals n.Id
            where r.PaymentDate <= date select new { n.Kind, r.Amount }).ToArrayAsync(ct);
        var opening = await db.OpeningSubledgerBalances.Where(x => x.CutoverDate <= date).ToArrayAsync(ct);
        var settlements = await (from s in db.OpeningSettlements join o in db.OpeningSubledgerBalances on s.OpeningBalanceId equals o.Id
            where s.PaymentDate <= date select new { o.Kind, s.Amount }).ToArrayAsync(ct);
        var openingJournal = await db.JournalEntries.Where(x => x.SourceType == "OpeningBalances").Select(x => (DateOnly?)x.JournalDate).SingleOrDefaultAsync(ct);
        var skuPurchases = await db.SkuMovements.Where(m => m.Kind == "Purchase" && m.MovementDate <= date).ToListAsync(ct);
        var purchasePayable = skuPurchases.Sum(m => m.TotalAmount) + purchases.Where(x => x.BillNumber != null && (!openingJournal.HasValue || x.PurchaseDate > openingJournal.Value)).Sum(x => x.TotalAmount);
        var ar = sales.Sum(x => x.TotalAmount) + invoices.Sum(i => i.TotalAmount) - receipts - advanceAmount + advanceRefunds - notes.Where(x => x.Kind == "Sale").Sum(x => x.TotalAmount)
            + refunds.Where(x => x.Kind == "Sale").Sum(x => x.Amount) + opening.Where(x => x.Kind == "Customer").Sum(x => x.Amount)
            - settlements.Where(x => x.Kind == "Customer").Sum(x => x.Amount);
        var ap = purchasePayable - payments - notes.Where(x => x.Kind == "Purchase").Sum(x => x.TotalAmount)
            + refunds.Where(x => x.Kind == "Purchase").Sum(x => x.Amount) + opening.Where(x => x.Kind == "Vendor").Sum(x => x.Amount)
            - settlements.Where(x => x.Kind == "Vendor").Sum(x => x.Amount);
        // Derive historical stock from purchase/sale/return/write-off events, not today's IsSold flag.
        var invoiceCosts = await (from line in db.JournalLines join entry in db.JournalEntries on line.JournalEntryId equals entry.Id
            join account in db.ChartAccounts on line.AccountId equals account.Id
            where entry.SourceType == "SalesInvoice" && account.Code == "1200" && entry.JournalDate <= date
            select new { entry.SourceId, Cost = line.Credit - line.Debit }).ToListAsync(ct);
        var skuOpening = await db.SkuMovements.Where(m => m.Kind == "OpeningStock" && m.MovementDate <= date).SumAsync(m => (decimal?)m.InventoryValue,ct) ?? 0m;
        var stock = skuOpening + skuPurchases.Sum(m => m.InventoryValue) + purchases.Sum(x => x.PurchasePrice - x.Discount)
            - sales.Sum(x => x.ProductPrice) - invoiceCosts.Sum(c => c.Cost)
            + notes.Where(x => x.Kind == "Sale" && x.Disposition == "Restock").Sum(x => sales.FirstOrDefault(s => s.Id == x.SourceId)?.ProductPrice ?? invoiceCosts.Where(c => c.SourceId == x.SourceId.ToString()).Sum(c => c.Cost))
            - notes.Where(x => x.Kind == "Purchase").Sum(x => x.TaxableAmount)
            - (await db.InventoryAdjustments.Where(x => x.AdjustmentDate <= date).SumAsync(x => (decimal?)x.Cost, ct) ?? 0);
        stock -= await db.SkuMovements.Where(m => m.Kind == "WriteOff" && m.MovementDate <= date).SumAsync(m => (decimal?)-m.InventoryValue,ct) ?? 0m;
        var skuPurchaseIds = skuPurchases.Select(m => m.Id).ToArray();
        stock += notes.Where(n => n.Kind == "Purchase" && skuPurchaseIds.Contains(n.SourceId)).Sum(n => n.TaxableAmount);
        stock += await db.SkuMovements.Where(m => m.Kind == "SupplierReturn" && m.MovementDate <= date).SumAsync(m => (decimal?)m.InventoryValue,ct) ?? 0m;
        async Task<decimal> Ledger(string code, bool credit)
        {
            var net = await (from l in db.JournalLines join e in db.JournalEntries on l.JournalEntryId equals e.Id
                join a in db.ChartAccounts on l.AccountId equals a.Id where e.JournalDate <= date && a.Code == code
                select (decimal?)(l.Debit - l.Credit)).SumAsync(ct) ?? 0;
            return credit ? -net : net;
        }
        var arGl = await Ledger("1100", false); var apGl = await Ledger("2000", true); var stockGl = await Ledger("1200", false);
        return new(date, ar, arGl, ap, apGl, stock, stockGl, ar - arGl, ap - apGl, stock - stockGl);
    }
}
