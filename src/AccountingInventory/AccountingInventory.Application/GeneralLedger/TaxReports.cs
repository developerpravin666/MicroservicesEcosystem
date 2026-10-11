using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record TaxReportRateSummary(
    decimal CgstRate,
    decimal SgstRate,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal TotalTax,
    decimal IgstRate = 0m,
    decimal IgstAmount = 0m);

public sealed record TaxReportSummary(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<TaxReportRateSummary> Sales,
    IReadOnlyList<TaxReportRateSummary> Purchases,
    decimal SalesTaxable,
    decimal OutputTax,
    decimal PurchaseTaxable,
    decimal InputTaxCredit,
    decimal OutputTaxLedger,
    decimal InputTaxCreditLedger,
    decimal OutputTaxDifference,
    decimal InputTaxCreditDifference,
    decimal NetTaxPayable);

public sealed record GetTaxReportQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<TaxReportSummary>;

/// <summary>GST transaction summary reconciled to the output GST and input credit control accounts.</summary>
public sealed class GetTaxReportHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetTaxReportQuery, TaxReportSummary>
{
    public async Task<TaxReportSummary> Handle(GetTaxReportQuery request, CancellationToken ct)
    {
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? new DateOnly(to.Year, 1, 1);
        if (fromDate > to) throw new ConflictException("From date must be on or before to date.");

        var singleSalesResult = await db.SalesProducts
            .Where(s => !s.IsDeleted)
            .Where(s => s.SaleDate >= fromDate && s.SaleDate <= to)
            .GroupBy(s => new { s.CgstRate, s.SgstRate })
            .Select(g => new
            {
                CgstRate = g.Key.CgstRate,
                SgstRate = g.Key.SgstRate,
                TaxableAmount = g.Sum(e => e.TaxableAmount),
                CgstAmount = g.Sum(e => e.CgstAmount),
                SgstAmount = g.Sum(e => e.SgstAmount)
            })
            .OrderBy(e0 => e0.CgstRate + e0.SgstRate)
            .ToListAsync(ct);

        var sales = singleSalesResult.Select(x => new TaxReportRateSummary(
            x.CgstRate,
            x.SgstRate,
            x.TaxableAmount,
            x.CgstAmount,
            x.SgstAmount,
            x.CgstAmount + x.SgstAmount,
            0m,
            0m
        )).ToList();

        // Include Multi-line sales invoices
        var multiSalesResult = await db.SalesInvoices
            .Where(s => s.InvoiceDate >= fromDate && s.InvoiceDate <= to)
            .SelectMany(s => s.Lines)
            .GroupBy(l => new { l.CgstRate, l.SgstRate, l.IgstRate })
            .Select(g => new
            {
                g.Key.CgstRate,
                g.Key.SgstRate,
                g.Key.IgstRate,
                TaxableAmount = g.Sum(e => e.TaxableAmount),
                CgstAmount = g.Sum(e => e.CgstAmount),
                SgstAmount = g.Sum(e => e.SgstAmount),
                IgstAmount = g.Sum(e => e.IgstAmount)
            })
            .ToListAsync(ct);

        foreach (var row in multiSalesResult)
        {
            sales.Add(new TaxReportRateSummary(
                row.CgstRate,
                row.SgstRate,
                row.TaxableAmount,
                row.CgstAmount,
                row.SgstAmount,
                row.CgstAmount + row.SgstAmount + row.IgstAmount,
                row.IgstRate,
                row.IgstAmount));
        }

        var purchaseRows = await db.Products.AsNoTracking()
            .Where(item => !item.IsOpeningStock && item.PurchaseDate >= fromDate && item.PurchaseDate <= to)
            .Select(item => new { item.Cgst, item.Sgst, item.PurchasePrice, item.Discount, item.TotalAmount })
            .ToListAsync(ct);

        var purchases = purchaseRows.GroupBy(item => new { item.Cgst, item.Sgst }).Select(group =>
        {
            var taxable = group.Sum(item => item.PurchasePrice - item.Discount);
            var cgst = group.Sum(item => decimal.Round((item.PurchasePrice - item.Discount) * item.Cgst / 100m, 2, MidpointRounding.AwayFromZero));
            var sgst = group.Sum(item => decimal.Round((item.PurchasePrice - item.Discount) * item.Sgst / 100m, 2, MidpointRounding.AwayFromZero));
            return new TaxReportRateSummary(
                group.Key.Cgst,
                group.Key.Sgst,
                taxable,
                cgst,
                sgst,
                group.Sum(item => item.TotalAmount - (item.PurchasePrice - item.Discount)),
                0m,
                0m);
        }).OrderBy(row => row.CgstRate + row.SgstRate).ToList();

        var skuPurchases = await db.SkuMovements.AsNoTracking().Where(m => m.Kind == "Purchase" && m.MovementDate >= fromDate && m.MovementDate <= to).ToListAsync(ct);
        purchases.AddRange(skuPurchases.Select(m => new TaxReportRateSummary(m.CgstRate,m.SgstRate,m.InventoryValue,m.CgstAmount,m.SgstAmount,
            m.CgstAmount+m.SgstAmount+m.IgstAmount,m.IgstRate,m.IgstAmount)));
        var notes = await db.InvoiceCorrections.AsNoTracking().Where(x => x.NoteDate >= fromDate && x.NoteDate <= to).ToListAsync(ct);
        var correctedInvoiceIds = notes.Where(n => n.Kind == "Sale").Select(n => n.SourceId).ToArray();
        var correctedInvoices = await db.SalesInvoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => correctedInvoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var purchaseNoteIds=notes.Where(n => n.Kind == "Purchase").Select(n => n.SourceId).ToArray();
        var correctedSkuPurchases=await db.SkuMovements.Where(m => purchaseNoteIds.Contains(m.Id) && m.Kind == "Purchase").ToDictionaryAsync(m => m.Id,ct);
        foreach (var note in notes)
        {
            if (note.Kind == "Sale" && correctedInvoices.TryGetValue(note.SourceId, out var invoice))
            {
                // Full invoice returns reverse each original rate bucket, including mixed GST rates.
                foreach (var line in invoice.Lines)
                    sales.Add(new(line.CgstRate, line.SgstRate, -line.TaxableAmount, -line.CgstAmount,
                        -line.SgstAmount, -(line.CgstAmount + line.SgstAmount + line.IgstAmount), line.IgstRate, -line.IgstAmount));
                continue;
            }
            if(note.Kind == "Purchase" && correctedSkuPurchases.TryGetValue(note.SourceId,out var skuPurchase)) {
                purchases.Add(new(skuPurchase.CgstRate,skuPurchase.SgstRate,-note.TaxableAmount,-note.CgstAmount,-note.SgstAmount,
                    -(note.CgstAmount+note.SgstAmount+note.IgstAmount),skuPurchase.IgstRate,-note.IgstAmount));
                continue;
            }
            var rows = note.Kind == "Sale" ? sales : purchases;
            rows.Add(new(note.CgstRate, note.SgstRate, -note.TaxableAmount, -note.CgstAmount, -note.SgstAmount,
                -(note.CgstAmount + note.SgstAmount + note.IgstAmount), 0m, -note.IgstAmount));
        }
        sales = Combine(sales);
        purchases = Combine(purchases);

        async Task<(decimal Debit, decimal Credit)> LedgerMovement(string code)
        {
            var row = await (from line in db.JournalLines.AsNoTracking()
                             join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                             join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
                             where account.Code == code && entry.JournalDate >= fromDate && entry.JournalDate <= to && entry.SourceType != "OpeningBalances"
                             group line by account.Code into rows
                             select new { Debit = rows.Sum(line => line.Debit), Credit = rows.Sum(line => line.Credit) })
                .FirstOrDefaultAsync(ct);
            return (row?.Debit ?? 0m, row?.Credit ?? 0m);
        }

        var outputLedger = await LedgerMovement("2100");
        var inputLedger = await LedgerMovement("2200");
        var outputTax = sales.Sum(row => row.TotalTax);
        var inputTax = purchases.Sum(row => row.TotalTax);
        var outputLedgerNet = outputLedger.Credit - outputLedger.Debit;
        var inputLedgerNet = inputLedger.Debit - inputLedger.Credit;
        return new TaxReportSummary(fromDate, to, sales, purchases,
            sales.Sum(row => row.TaxableAmount), outputTax, purchases.Sum(row => row.TaxableAmount), inputTax,
            outputLedgerNet, inputLedgerNet, outputTax - outputLedgerNet, inputTax - inputLedgerNet,
            outputTax - inputTax);
    }

    private static List<TaxReportRateSummary> Combine(IEnumerable<TaxReportRateSummary> rows) => rows
        .GroupBy(x => new { x.CgstRate, x.SgstRate, x.IgstRate })
        .Select(g => new TaxReportRateSummary(
            g.Key.CgstRate,
            g.Key.SgstRate,
            g.Sum(x => x.TaxableAmount),
            g.Sum(x => x.CgstAmount),
            g.Sum(x => x.SgstAmount),
            g.Sum(x => x.TotalTax),
            g.Key.IgstRate,
            g.Sum(x => x.IgstAmount)))
        .OrderBy(x => x.CgstRate + x.SgstRate + x.IgstRate)
        .ToList();
}
