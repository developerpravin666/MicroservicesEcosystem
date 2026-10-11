using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public interface IBusinessPermissionRequest { string BusinessPermission { get; } }
public sealed record ReturnInvoiceCommand(string Kind, Guid SourceId, DateOnly NoteDate, string Reason, string Disposition)
    : IRequest<InvoiceCorrection>, IBusinessPermissionRequest
{ public string BusinessPermission => Kind == "Sale" ? "sales.manage" : "purchases.manage"; }
public sealed record RefundCorrectionCommand(Guid CorrectionId, DateOnly PaymentDate, decimal Amount, string PaymentMode, string? Reference)
    : IRequest<CorrectionRefundSummary>;
public sealed record CorrectionRefundSummary(Guid Id, Guid CorrectionId, DateOnly PaymentDate, decimal Amount, string PaymentMode, string Reference);
public sealed record GetInvoiceCorrectionsQuery() : IRequest<IReadOnlyList<CorrectionSummary>>;
public sealed record CorrectionSummary(Guid Id, string Kind, Guid SourceId, Guid PartyId, string BillNumber,
    string NoteNumber, DateOnly NoteDate, string Reason, string Disposition, decimal TotalAmount, decimal Refunded, decimal RefundAvailable,
    decimal TaxableAmount, decimal CgstRate, decimal SgstRate, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount = 0m);

public sealed class InvoiceCorrectionHandler(IAccountingInventoryDbContext db, IRequestIdentity identity) :
    IRequestHandler<ReturnInvoiceCommand, InvoiceCorrection>, IRequestHandler<RefundCorrectionCommand, CorrectionRefundSummary>,
    IRequestHandler<GetInvoiceCorrectionsQuery, IReadOnlyList<CorrectionSummary>>
{
    public async Task<InvoiceCorrection> Handle(ReturnInvoiceCommand q, CancellationToken ct)
    {
        if (q.Kind is not ("Sale" or "Purchase")) throw new ConflictException("Choose Sale or Purchase.");
        if (await db.InvoiceCorrections.AnyAsync(x => x.Kind == q.Kind && x.SourceId == q.SourceId, ct))
            throw new ConflictException("This invoice unit has already been returned or cancelled.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.NoteDate, ct);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);
        if(q.Kind == "Purchase") {
            var skuPurchase = await db.SkuMovements.SingleOrDefaultAsync(m => m.Id == q.SourceId && m.Kind == "Purchase",ct);
            if(skuPurchase is not null) return await ReturnSkuPurchase(q,skuPurchase,accounts,ct);
        }
        Product? product = null;
        SalesInvoice? invoice = null;
        var invoiceProducts = new List<Product>();
        InvoiceCorrection note;
        if (q.Kind == "Sale")
        {
            invoice = await db.SalesInvoices.Include(i => i.Lines).SingleOrDefaultAsync(i => i.Id == q.SourceId, ct);
            if (invoice is not null)
            {
                if (invoice.IsCancelled || q.NoteDate < invoice.InvoiceDate || q.Disposition is not ("Restock" or "WriteOff"))
                    throw new ConflictException("Choose an unreturned invoice and a note date on/after its invoice date.");
                if (await db.SalesInvoiceReceipts.AnyAsync(r => r.InvoiceId == invoice.Id && r.PaymentDate > q.NoteDate, ct))
                    throw new ConflictException("Return cannot precede an invoice receipt or advance application.");
                var ids = invoice.Lines.Where(l => l.ProductId.HasValue).Select(l => l.ProductId!.Value).ToArray();
                invoiceProducts = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
                if (invoiceProducts.Count != ids.Length || invoiceProducts.Any(p => !p.IsSold || !p.IsActive))
                    throw new ConflictException("Invoice inventory is no longer available for return.");
                foreach (var item in invoiceProducts) item.MarkAvailable();
                note = InvoiceCorrection.Create("Sale", invoice.Id, invoice.CustomerId, invoice.BillNumber, q.NoteDate, q.Reason,
                    q.Disposition, invoice.TaxableAmount, 0m, 0m, invoice.CgstAmount, invoice.SgstAmount, invoice.TotalAmount, invoice.IgstAmount);
                var skuSales = await db.SkuMovements.Where(m => m.Kind == "Sale" && m.SourceId == invoice.Id).ToListAsync(ct);
                foreach (var movement in skuSales) {
                    var sku = await db.StockSkus.SingleAsync(s => s.Id == movement.SkuId,ct);
                    if(q.Disposition == "Restock") sku.Receive(-movement.Quantity,-movement.InventoryValue,q.NoteDate);
                    else {
                        sku.RecordDiscardedReturn(-movement.Quantity,q.NoteDate);
                        db.SkuMovements.Add(SkuMovement.Create(sku.Id,q.NoteDate,"ReturnWriteOff",note.Id,movement.Quantity,movement.InventoryValue,q.Reason));
                    }
                    db.SkuMovements.Add(SkuMovement.Create(sku.Id,q.NoteDate,"CustomerReturn",note.Id,-movement.Quantity,-movement.InventoryValue,q.Reason));
                }
                invoice.Cancel();
            }
            else
            {
            var sale = await db.SalesProducts.SingleOrDefaultAsync(x => x.Id == q.SourceId, ct)
                ?? throw new NotFoundException("Sales invoice was not found.");
            if (sale.IsReturned || q.NoteDate < sale.SaleDate || q.Disposition is not ("Restock" or "WriteOff"))
                throw new ConflictException("Use a date on/after the invoice and Restock or WriteOff for an unreturned sale.");
            if (await db.SalesReceipts.AnyAsync(x => x.SalesProductId == sale.Id && x.PaymentDate > q.NoteDate, ct))
                throw new ConflictException("The return cannot precede an existing invoice receipt.");
            if (!Guid.TryParse(sale.ProductId, out var id)) throw new ConflictException("The invoice stock reference is invalid.");
            product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Stock was not found.");
            if (!product.IsSold) throw new ConflictException("The stock is no longer assigned to this sale.");
            note = InvoiceCorrection.Create("Sale", sale.Id, sale.CustomerId, sale.BillNumber, q.NoteDate,
                q.Reason, q.Disposition, sale.TaxableAmount, sale.CgstRate, sale.SgstRate, sale.CgstAmount, sale.SgstAmount, sale.TotalAmount);
            sale.MarkReturned(); product.MarkAvailable();
            }
        }
        else
        {
            product = await db.Products.SingleOrDefaultAsync(x => x.Id == q.SourceId, ct) ?? throw new NotFoundException("Purchase unit was not found.");
            if (q.NoteDate < product.PurchaseDate || product.IsSold || !product.IsActive || q.Disposition != "Supplier")
                throw new ConflictException("Only available stock can be returned to the supplier, on/after its purchase date.");
            var stockSaleIds = await db.SalesProducts.Where(x => x.ProductId == product.Id.ToString()).Select(x => x.Id).ToArrayAsync(ct);
            var stockInvoiceIds = await db.SalesInvoiceLines.Where(x => x.ProductId == product.Id).Select(x => x.SalesInvoiceId).ToArrayAsync(ct);
            if (await db.InvoiceCorrections.AnyAsync(x => x.Kind == "Sale" && (stockSaleIds.Contains(x.SourceId) || stockInvoiceIds.Contains(x.SourceId)) && x.NoteDate > q.NoteDate, ct))
                throw new ConflictException("Supplier return cannot precede the customer return.");
            var taxable = decimal.Round(product.PurchasePrice - product.Discount, 2, MidpointRounding.AwayFromZero);
            var cgst = decimal.Round(taxable * product.Cgst / 100m, 2, MidpointRounding.AwayFromZero);
            note = InvoiceCorrection.Create("Purchase", product.Id, product.VendorId, product.BillNumber ?? product.Id.ToString(),
                q.NoteDate, q.Reason, "Supplier", taxable, product.Cgst, product.Sgst, cgst, product.TotalAmount - taxable - cgst, product.TotalAmount);
            product.WriteOff();
        }
        // Reverse the exact original source journal, including its captured cost and tax.
        var sourceType = invoice is not null ? "SalesInvoice" : q.Kind == "Sale" ? "Sale" : "PurchaseProduct";
        var original = await db.JournalEntries
            .SingleOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == q.SourceId.ToString(), ct);
        if (original is null && note.TotalAmount > 0)
            throw new ConflictException("This legacy invoice has no source journal. Reconcile it before returning it.");
        if (original is not null)
        {
            var originalLines = await db.JournalLines.Where(x => x.JournalEntryId == original.Id).ToArrayAsync(ct);
            if (await db.JournalEntries.AnyAsync(x => x.ReversalOfJournalEntryId == original.Id, ct))
                throw new ConflictException("The source journal has already been reversed.");
            var reversal = JournalEntry.Post(q.NoteDate, $"{note.NoteNumber}: {note.Reason}", "InvoiceCorrection", note.Id.ToString(),
                originalLines.Select(x => (x.AccountId, x.Credit, x.Debit, (string?)note.Reason)).ToArray(), original.Id);
            foreach (var line in reversal.Lines)
                line.AssignDimension(originalLines.First(x => x.AccountId == line.AccountId && x.Credit == line.Debit && x.Debit == line.Credit).DimensionId);
            LedgerPosting.Add(db, reversal);
        }
        if (q.Kind == "Sale" && q.Disposition == "WriteOff")
        {
            if (invoice is not null) { foreach (var item in invoiceProducts) item.WriteOff(); }
            else product!.WriteOff();
            var cost = original is null ? 0 : await db.JournalLines.Where(x => x.JournalEntryId == original.Id && x.AccountId == accounts["5000"])
                .SumAsync(x => x.Debit, ct);
            if (cost > 0) LedgerPosting.Add(db, JournalEntry.Post(q.NoteDate, "Returned stock write-off", "ReturnWriteOff", note.Id.ToString(),
                [(accounts["5100"], cost, 0m, (string?)q.Reason), (accounts["1200"], 0m, cost, (string?)q.Reason)]));
        }
        db.InvoiceCorrections.Add(note);
        await db.SaveChangesAsync(ct);
        return note;
    }

    private async Task<InvoiceCorrection> ReturnSkuPurchase(ReturnInvoiceCommand q,SkuMovement purchase,IReadOnlyDictionary<string,Guid> accounts,CancellationToken ct)
    {
        if(q.Disposition != "Supplier" || q.NoteDate < purchase.MovementDate) throw new ConflictException("Use Supplier disposition and a date on/after purchase.");
        if(await db.PurchasePayments.AnyAsync(p => p.VendorId == purchase.VendorId && p.BillNumber.ToLower() == purchase.BillNumber!.ToLower() && p.PaymentDate > q.NoteDate,ct))
            throw new ConflictException("Supplier return cannot precede bill payments.");
        var sku=await db.StockSkus.SingleAsync(s => s.Id == purchase.SkuId,ct);
        var cost=sku.Issue(purchase.Quantity,q.NoteDate);
        var note=InvoiceCorrection.Create("Purchase",purchase.Id,purchase.VendorId!.Value,purchase.BillNumber!,q.NoteDate,q.Reason,"Supplier",
            purchase.InventoryValue,purchase.CgstRate,purchase.SgstRate,purchase.CgstAmount,purchase.SgstAmount,purchase.TotalAmount,purchase.IgstAmount);
        db.InvoiceCorrections.Add(note);
        db.SkuMovements.Add(SkuMovement.Create(sku.Id,q.NoteDate,"SupplierReturn",note.Id,-purchase.Quantity,-cost,q.Reason));
        var lines=new List<(Guid,decimal,decimal,string?)>();
        if(purchase.TotalAmount>0) lines.Add((accounts["2000"],purchase.TotalAmount,0,"Supplier credit"));
        if(cost>0) lines.Add((accounts["1200"],0,cost,"Accessory stock returned"));
        var gst=purchase.CgstAmount+purchase.SgstAmount+purchase.IgstAmount;
        if(gst>0) lines.Add((accounts["2200"],0,gst,"Input GST reversed"));
        var variance=cost-purchase.InventoryValue;
        if(variance!=0) lines.Add((accounts["5000"],Math.Max(variance,0),Math.Max(-variance,0),"Purchase return cost variance"));
        if(lines.Count>0) LedgerPosting.Add(db,JournalEntry.Post(q.NoteDate,q.Reason,"InvoiceCorrection",note.Id.ToString(),lines));
        await db.SaveChangesAsync(ct);return note;
    }
    public async Task<CorrectionRefundSummary> Handle(RefundCorrectionCommand q, CancellationToken ct)
    {
        var note = await db.InvoiceCorrections.SingleOrDefaultAsync(x => x.Id == q.CorrectionId, ct)
            ?? throw new NotFoundException("Credit/debit note was not found.");
        await AccountingPermissionGate.EnsureAsync(db, identity.UserId, note.Kind == "Sale" ? "sales.manage" : "purchases.manage", ct);
        if (q.PaymentDate < note.NoteDate) throw new ConflictException("Refunds cannot precede the note.");
        await LedgerPosting.EnsurePeriodOpenAsync(db, q.PaymentDate, ct);
        var available = await RefundAvailable(note, ct, q.PaymentDate);
        var refund = CorrectionRefund.Create(note.Id, q.PaymentDate, q.Amount, q.PaymentMode, q.Reference);
        if (refund.Amount > available) throw new ConflictException($"Refund exceeds the available paid credit of {available:0.00}.");
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);
        var cash = accounts[q.PaymentMode == "Cash" ? "1000" : "1010"];
        var control = accounts[note.Kind == "Sale" ? "1100" : "2000"];
        var lines = note.Kind == "Sale"
            ? new[] { (control, q.Amount, 0m, (string?)"Customer refund"), (cash, 0m, q.Amount, (string?)q.Reference) }
            : new[] { (cash, q.Amount, 0m, (string?)q.Reference), (control, 0m, q.Amount, (string?)"Supplier refund") };
        db.CorrectionRefunds.Add(refund);
        LedgerPosting.Add(db, JournalEntry.Post(q.PaymentDate, "Invoice refund", "CorrectionRefund", refund.Id.ToString(), lines));
        await db.SaveChangesAsync(ct);
        return new(refund.Id, refund.CorrectionId, refund.PaymentDate, refund.Amount, refund.PaymentMode, refund.Reference);
    }
    public async Task<IReadOnlyList<CorrectionSummary>> Handle(GetInvoiceCorrectionsQuery q, CancellationToken ct)
    {
        var notes = await db.InvoiceCorrections.AsNoTracking().OrderByDescending(x => x.NoteDate).ToListAsync(ct);
        var result = new List<CorrectionSummary>();
        foreach (var n in notes)
            result.Add(new(n.Id, n.Kind, n.SourceId, n.PartyId, n.BillNumber, n.NoteNumber, n.NoteDate, n.Reason, n.Disposition,
                n.TotalAmount, await db.CorrectionRefunds.Where(x => x.CorrectionId == n.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
                await RefundAvailable(n, ct), n.TaxableAmount, n.CgstRate, n.SgstRate, n.CgstAmount, n.SgstAmount, n.IgstAmount));
        return result;
    }
    private async Task<decimal> RefundAvailable(InvoiceCorrection n, CancellationToken ct, DateOnly? asOf = null)
    {
        var date = asOf ?? DateOnly.MaxValue;
        if (n.Kind == "Sale")
        {
            var invoicePaid = await db.SalesInvoiceReceipts.Where(r => r.InvoiceId == n.SourceId && r.PaymentDate <= date).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
            var paid = await db.SalesReceipts.Where(x => x.SalesProductId == n.SourceId && x.PaymentDate <= date).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var refunded = await db.CorrectionRefunds.Where(x => x.CorrectionId == n.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            return Math.Max(0, Math.Min(n.TotalAmount, paid + invoicePaid) - refunded);
        }
        var bill = n.BillNumber.ToLower();
        var total = await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db).Where(x => x.VendorId == n.PartyId && x.BillNumber != null && x.BillNumber.ToLower() == bill)
            .SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var credits = await db.InvoiceCorrections.Where(x => x.Kind == "Purchase" && x.PartyId == n.PartyId && x.BillNumber.ToLower() == bill && x.NoteDate <= date)
            .SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var paidBill = await db.PurchasePayments.Where(x => x.VendorId == n.PartyId && x.BillNumber.ToLower() == bill && x.PaymentDate <= date)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var refundedBill = await (from refund in db.CorrectionRefunds join note in db.InvoiceCorrections on refund.CorrectionId equals note.Id
            where note.Kind == "Purchase" && note.PartyId == n.PartyId && note.BillNumber.ToLower() == bill
            select (decimal?)refund.Amount).SumAsync(ct) ?? 0;
        var refundedNote = await db.CorrectionRefunds.Where(x => x.CorrectionId == n.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        return Math.Max(0, Math.Min(n.TotalAmount - refundedNote, paidBill - (total - credits) - refundedBill));
    }
}
