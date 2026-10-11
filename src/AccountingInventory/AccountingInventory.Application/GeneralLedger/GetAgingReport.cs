using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record AgingInvoiceSummary(string InvoiceNumber, Guid CounterpartyId, string CounterpartyName,
    DateOnly InvoiceDate, DateOnly DueDate, int AgeDays, string AgeBucket, decimal OriginalAmount, decimal AmountPaid, decimal Balance);
public sealed record AgingBucketSummary(string Bucket, int InvoiceCount, decimal Balance);
public sealed record AgingReportSummary(DateOnly AsOfDate,
    PagedResult<AgingInvoiceSummary> Receivables, IReadOnlyList<AgingBucketSummary> ReceivableBuckets,
    decimal TotalReceivables, PagedResult<AgingInvoiceSummary> Payables,
    IReadOnlyList<AgingBucketSummary> PayableBuckets, decimal TotalPayables);
public sealed record GetAgingReportQuery(DateOnly? AsOfDate = null, int Page = 1, int PageSize = 20, string? Search = null, Guid? PartyId = null)
    : IRequest<AgingReportSummary>;

public sealed class GetAgingReportHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetAgingReportQuery, AgingReportSummary>
{
    private static readonly string[] BucketNames = ["0–30 days", "31–60 days", "61–90 days", "90+ days"];

    public async Task<AgingReportSummary> Handle(GetAgingReportQuery request, CancellationToken ct)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var cutover = await db.JournalEntries.Where(x => x.SourceType == "OpeningBalances").Select(x => (DateOnly?)x.JournalDate).SingleOrDefaultAsync(ct);
        var sales = await (from sale in db.SalesProducts.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on sale.CustomerId equals customer.Id
            where sale.SaleDate <= asOf
            select new { sale.Id, sale.BillNumber, sale.CustomerId, CustomerName = customer.Name, sale.SaleDate, sale.DueDate, sale.TotalAmount })
            .ToListAsync(ct);
        var saleIds = sales.Select(sale => sale.Id).ToArray();
        var notes = await db.InvoiceCorrections.AsNoTracking().Where(x => x.NoteDate <= asOf).ToListAsync(ct);
        var receipts = await db.SalesReceipts.AsNoTracking()
            .Where(receipt => saleIds.Contains(receipt.SalesProductId) && receipt.PaymentDate <= asOf)
            .GroupBy(receipt => receipt.SalesProductId)
            .Select(group => new { SaleId = group.Key, Amount = group.Sum(receipt => receipt.Amount) })
            .ToDictionaryAsync(row => row.SaleId, row => row.Amount, ct);
        var receivableRows = sales.Select(sale => MakeRow(sale.BillNumber, sale.CustomerId, sale.CustomerName,
                sale.SaleDate, sale.DueDate, sale.TotalAmount - notes.Where(x => x.Kind == "Sale" && x.SourceId == sale.Id).Sum(x => x.TotalAmount), receipts.GetValueOrDefault(sale.Id), asOf))
            .Where(row => row.Balance > 0m).ToArray();

        var invoices = await (from invoice in db.SalesInvoices.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on invoice.CustomerId equals customer.Id
            where invoice.InvoiceDate <= asOf
            select new { invoice.Id, invoice.BillNumber, invoice.CustomerId, customer.Name, invoice.InvoiceDate, invoice.DueDate, invoice.TotalAmount }).ToListAsync(ct);
        var invoiceIds = invoices.Select(i => i.Id).ToArray();
        var invoiceReceipts = await db.SalesInvoiceReceipts.Where(r => invoiceIds.Contains(r.InvoiceId) && r.PaymentDate <= asOf)
            .GroupBy(r => r.InvoiceId).Select(g => new { Id = g.Key, Amount = g.Sum(r => r.Amount) }).ToDictionaryAsync(r => r.Id, r => r.Amount, ct);
        receivableRows = receivableRows.Concat(invoices.Select(i => MakeRow(i.BillNumber, i.CustomerId, i.Name, i.InvoiceDate, i.DueDate,
            i.TotalAmount - notes.Where(n => n.Kind == "Sale" && n.SourceId == i.Id).Sum(n => n.TotalAmount), invoiceReceipts.GetValueOrDefault(i.Id), asOf))
            .Where(r => r.Balance > 0)).ToArray();

        var purchaseGroups = await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db)
            .Where(product => product.BillNumber != null && product.PurchaseDate <= asOf && (!cutover.HasValue || product.PurchaseDate > cutover.Value))
            .GroupBy(product => new { product.VendorId, product.BillNumber })
            .Select(group => new
            {
                group.Key.VendorId,
                BillNumber = group.Key.BillNumber!,
                BillDate = group.Min(product => product.PurchaseDate),
                DueDate = group.Max(product => product.DueDate),
                Total = group.Sum(product => product.TotalAmount)
            }).ToListAsync(ct);
        var vendorIds = purchaseGroups.Select(group => group.VendorId).Distinct().ToArray();
        var vendorNames = await db.Vendors.AsNoTracking().Where(vendor => vendorIds.Contains(vendor.Id))
            .ToDictionaryAsync(vendor => vendor.Id, vendor => vendor.Name, ct);
        var billKeys = purchaseGroups.Select(group => group.BillNumber.ToLower()).Distinct().ToArray();
        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => vendorIds.Contains(payment.VendorId) && billKeys.Contains(payment.BillNumber.ToLower())
                && payment.PaymentDate <= asOf)
            .GroupBy(payment => new { payment.VendorId, BillNumber = payment.BillNumber.ToLower() })
            .Select(group => new { group.Key.VendorId, group.Key.BillNumber, Amount = group.Sum(payment => payment.Amount) })
            .ToListAsync(ct);
        var paidByBill = payments.ToDictionary(payment => (payment.VendorId, payment.BillNumber), payment => payment.Amount);
        var payableRows = purchaseGroups.Select(group => MakeRow(group.BillNumber, group.VendorId,
                vendorNames.GetValueOrDefault(group.VendorId, string.Empty), group.BillDate, group.DueDate,
                group.Total - notes.Where(x => x.Kind == "Purchase" && x.PartyId == group.VendorId && x.BillNumber.Equals(group.BillNumber, StringComparison.OrdinalIgnoreCase)).Sum(x => x.TotalAmount),
                paidByBill.GetValueOrDefault((group.VendorId, group.BillNumber.ToLower())), asOf))
            .Where(row => row.Balance > 0m).ToArray();

        var opening = await db.OpeningSubledgerBalances.AsNoTracking().Where(x => x.CutoverDate <= asOf).ToArrayAsync(ct);
        foreach (var item in opening)
        {
            var paid = await db.OpeningSettlements.Where(x => x.OpeningBalanceId == item.Id && x.PaymentDate <= asOf).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var name = item.Kind == "Customer" ? await db.Customers.Where(x => x.Id == item.PartyId).Select(x => x.Name).FirstOrDefaultAsync(ct)
                : await db.Vendors.Where(x => x.Id == item.PartyId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            var row = MakeRow(item.Reference, item.PartyId, name ?? "", item.CutoverDate, item.DueDate, item.Amount, paid, asOf);
            if (row.Balance > 0)
            {
                if (item.Kind == "Customer") receivableRows = receivableRows.Append(row).ToArray();
                else payableRows = payableRows.Append(row).ToArray();
            }
        }
        if (request.PartyId.HasValue) { receivableRows = receivableRows.Where(r => r.CounterpartyId == request.PartyId.Value).ToArray(); payableRows = payableRows.Where(r => r.CounterpartyId == request.PartyId.Value).ToArray(); }
        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            receivableRows = receivableRows.Where(row => row.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.CounterpartyName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            payableRows = payableRows.Where(row => row.InvoiceNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || row.CounterpartyName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return new AgingReportSummary(asOf,
            Page(receivableRows, page, size), Buckets(receivableRows), receivableRows.Sum(row => row.Balance),
            Page(payableRows, page, size), Buckets(payableRows), payableRows.Sum(row => row.Balance));
    }

    private static AgingInvoiceSummary MakeRow(string invoiceNumber, Guid counterpartyId, string counterpartyName,
        DateOnly invoiceDate, DateOnly dueDate, decimal original, decimal paid, DateOnly asOf)
    {
        var age = Math.Max(0, asOf.DayNumber - dueDate.DayNumber);
        var bucket = age <= 30 ? BucketNames[0] : age <= 60 ? BucketNames[1] : age <= 90 ? BucketNames[2] : BucketNames[3];
        var balance = Math.Max(0m, original - paid);
        return new(invoiceNumber, counterpartyId, counterpartyName, invoiceDate, dueDate, age, bucket, original, paid, balance);
    }

    private static IReadOnlyList<AgingBucketSummary> Buckets(IEnumerable<AgingInvoiceSummary> rows)
    {
        var materialized = rows.ToArray();
        return BucketNames.Select(name => new AgingBucketSummary(name,
            materialized.Count(row => row.AgeBucket == name), materialized.Where(row => row.AgeBucket == name).Sum(row => row.Balance)))
            .ToArray();
    }

    private static PagedResult<AgingInvoiceSummary> Page(AgingInvoiceSummary[] rows, int page, int size)
    {
        var ordered = rows.OrderByDescending(row => row.AgeDays).ThenBy(row => row.InvoiceDate).ToArray();
        return new PagedResult<AgingInvoiceSummary>(ordered.Skip((page - 1) * size).Take(size).ToArray(), page, size,
            ordered.Length, ordered.Length == 0 ? 0 : (int)Math.Ceiling(ordered.Length / (double)size));
    }
}
