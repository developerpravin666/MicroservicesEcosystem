using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Accounting;

public sealed record GetPurchaseBillsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, string? SortDirection = null)
    : IRequest<PagedResult<PurchaseBillSummary>>;

public sealed record PurchaseBillSummary(
    string Id, Guid VendorId, string VendorName, string BillNumber, DateOnly BillDate, int PaymentTermsDays, DateOnly DueDate,
    decimal TotalAmount, decimal AmountPaid, decimal Balance, string PaymentStatus);

public sealed record PurchasePaymentSummary(
    Guid Id, decimal Amount, string PaymentMode, DateOnly PaymentDate,
    string? ReferenceNumber, string? Note);

public sealed record PurchaseBillDetails(PurchaseBillSummary Bill, IReadOnlyList<PurchasePaymentSummary> Payments);

public sealed record GetPurchaseBillDetailsQuery(Guid VendorId, string BillNumber)
    : IRequest<PurchaseBillDetails>;

public sealed class GetPurchaseBillsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPurchaseBillsQuery, PagedResult<PurchaseBillSummary>>
{
    public async Task<PagedResult<PurchaseBillSummary>> Handle(GetPurchaseBillsQuery request, CancellationToken cancellationToken)
    {
        var products = AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db).Where(product => product.BillNumber != null && !product.IsOpeningStock);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            products = products.Where(product => product.BillNumber!.ToLower().Contains(search)
                || db.Vendors.Any(vendor => vendor.Id == product.VendorId && vendor.Name.ToLower().Contains(search)));
        }

        var invoices = products.GroupBy(product => new { product.VendorId, BillNumber = product.BillNumber!.Trim().ToLower() })
            .Select(group => new
            {
                group.Key.VendorId,
                BillNumber = group.Min(product => product.BillNumber)!,
                BillDate = group.Min(product => product.PurchaseDate),
                PaymentTermsDays = group.Max(product => product.PaymentTermsDays),
                DueDate = group.Max(product => product.DueDate),
                TotalAmount = group.Sum(product => product.TotalAmount)
            });

        var totalCount = await invoices.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var invoiceRows = (from invoice in invoices
            join vendor in db.Vendors.AsNoTracking() on invoice.VendorId equals vendor.Id
            select new
            {
                invoice.VendorId, VendorName = db.InvoiceSnapshots.Where(s => s.Kind == "Purchase"
                    && db.Products.Any(p => p.Id == s.SourceId && p.VendorId == invoice.VendorId && p.BillNumber != null && p.BillNumber.ToLower() == invoice.BillNumber.ToLower()))
                    .OrderBy(s => s.CreatedAt).Select(s => s.PartyName).FirstOrDefault() ?? vendor.Name, invoice.BillNumber, invoice.BillDate,
                invoice.PaymentTermsDays, invoice.DueDate,
                TotalAmount = invoice.TotalAmount - (db.InvoiceCorrections.Where(n => n.Kind == "Purchase" && n.PartyId == invoice.VendorId
                    && n.BillNumber.ToLower() == invoice.BillNumber.ToLower()).Sum(n => (decimal?)n.TotalAmount) ?? 0),
                AmountPaid = db.PurchasePayments.Where(payment => payment.VendorId == invoice.VendorId
                    && payment.BillNumber.ToLower() == invoice.BillNumber.ToLower())
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            }).Select(row => new
            {
                row.VendorId, row.VendorName, row.BillNumber, row.BillDate, row.PaymentTermsDays,
                row.DueDate, row.TotalAmount, row.AmountPaid,
                Balance = Math.Max(0m, row.TotalAmount - row.AmountPaid),
                PaymentStatus = row.AmountPaid >= row.TotalAmount ? "Paid" : row.AmountPaid > 0m ? "Partially paid" : "Unpaid"
            });
        var rows = await GridSorting.Apply(invoiceRows, request.SortBy, request.SortDirection, "BillDate", true)
            .ThenBy(row => row.BillNumber).ThenBy(row => row.VendorId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = rows.Select(row => new PurchaseBillSummary($"{row.VendorId}:{row.BillNumber}",
            row.VendorId, row.VendorName, row.BillNumber, row.BillDate, row.PaymentTermsDays, row.DueDate,
            row.TotalAmount, row.AmountPaid, row.Balance, row.PaymentStatus)).ToArray();

        return new PagedResult<PurchaseBillSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}

public sealed class GetPurchaseBillDetailsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPurchaseBillDetailsQuery, PurchaseBillDetails>
{
    public async Task<PurchaseBillDetails> Handle(GetPurchaseBillDetailsQuery request, CancellationToken cancellationToken)
    {
        var normalizedBillNumber = request.BillNumber.Trim().ToLower();
        var invoice = await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db)
            .Where(product => product.VendorId == request.VendorId && !product.IsOpeningStock && product.BillNumber != null
                && product.BillNumber.ToLower() == normalizedBillNumber)
            .GroupBy(product => new { product.VendorId, BillNumber = product.BillNumber!.Trim().ToLower() })
            .Select(group => new
            {
                group.Key.VendorId,
                BillNumber = group.Min(product => product.BillNumber)!,
                BillDate = group.Min(product => product.PurchaseDate),
                PaymentTermsDays = group.Max(product => product.PaymentTermsDays),
                DueDate = group.Max(product => product.DueDate),
                TotalAmount = group.Sum(product => product.TotalAmount)
            }).FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
            throw new BuildingBlocks.Application.Exceptions.NotFoundException(
                $"Purchase bill '{request.BillNumber}' was not found for vendor '{request.VendorId}'.");

        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => payment.VendorId == request.VendorId
                && payment.BillNumber.ToLower() == normalizedBillNumber)
            .OrderByDescending(payment => payment.PaymentDate)
            .ThenByDescending(payment => payment.CreatedAt)
            .Select(payment => new PurchasePaymentSummary(payment.Id, payment.Amount, payment.PaymentMode,
                payment.PaymentDate, payment.ReferenceNumber, payment.Note))
            .ToListAsync(cancellationToken);
        var amountPaid = payments.Sum(payment => payment.Amount);
        var credited = await db.InvoiceCorrections.Where(n => n.Kind == "Purchase" && n.PartyId == request.VendorId && n.BillNumber.ToLower() == normalizedBillNumber)
            .SumAsync(n => (decimal?)n.TotalAmount, cancellationToken) ?? 0;
        var netTotal = invoice.TotalAmount - credited;
        var balance = Math.Max(0m, netTotal - amountPaid);
        var status = balance == 0m ? "Paid" : amountPaid > 0m ? "Partially paid" : "Unpaid";
        var vendorName = await db.InvoiceSnapshots.Where(s => s.Kind == "Purchase" && db.Products.Any(p => p.Id == s.SourceId
                && p.VendorId == request.VendorId && p.BillNumber != null && p.BillNumber.ToLower() == normalizedBillNumber))
            .OrderBy(s => s.CreatedAt).Select(s => s.PartyName).FirstOrDefaultAsync(cancellationToken)
            ?? await db.Vendors.AsNoTracking().Where(vendor => vendor.Id == invoice.VendorId)
            .Select(vendor => vendor.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var bill = new PurchaseBillSummary($"{invoice.VendorId}:{invoice.BillNumber}", invoice.VendorId, vendorName, invoice.BillNumber,
            invoice.BillDate, invoice.PaymentTermsDays, invoice.DueDate, netTotal, amountPaid, balance, status);
        return new PurchaseBillDetails(bill, payments);
    }
}
