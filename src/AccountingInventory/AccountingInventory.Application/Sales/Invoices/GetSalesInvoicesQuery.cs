using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Invoices;

public sealed record GetSalesInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    string? SortDirection = null) : IRequest<PagedResult<SalesInvoiceSummaryDto>>;

public sealed class GetSalesInvoicesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesInvoicesQuery, PagedResult<SalesInvoiceSummaryDto>>
{
    public async Task<PagedResult<SalesInvoiceSummaryDto>> Handle(GetSalesInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesInvoices
            .Include(i => i.Lines)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            var customerIds = await db.Customers.AsNoTracking()
                .Where(c => c.Name.ToLower().Contains(search) || c.Mobile.Contains(search))
                .Select(c => c.Id)
                .ToArrayAsync(cancellationToken);

            query = query.Where(i =>
                i.BillNumber.ToLower().Contains(search)
                || customerIds.Contains(i.CustomerId)
                || i.Lines.Any(l => l.ItemDescription.ToLower().Contains(search)
                    || (l.SerialNumber != null && l.SerialNumber.ToLower().Contains(search))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "invoicedate" => request.SortDirection == "asc" ? query.OrderBy(x => x.InvoiceDate) : query.OrderByDescending(x => x.InvoiceDate),
            "billnumber" => request.SortDirection == "asc" ? query.OrderBy(x => x.BillNumber) : query.OrderByDescending(x => x.BillNumber),
            "totalamount" => request.SortDirection == "asc" ? query.OrderBy(x => x.TotalAmount) : query.OrderByDescending(x => x.TotalAmount),
            "balance" => request.SortDirection == "asc" ? query.OrderBy(x => x.Balance) : query.OrderByDescending(x => x.Balance),
            _ => query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt)
        };

        var invoices = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var customerIdsForInvoices = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIdsForInvoices.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var items = invoices.Select(invoice =>
        {
            customers.TryGetValue(invoice.CustomerId, out var customer);
            return new SalesInvoiceSummaryDto(
                invoice.Id,
                invoice.BillNumber,
                invoice.CustomerId,
                customer?.Name ?? "Unknown",
                customer?.Mobile ?? "",
                customer?.Address ?? "",
                customer?.Email,
                invoice.InvoiceDate,
                invoice.PaymentTermsDays,
                invoice.DueDate,
                invoice.Lines.Count,
                invoice.SubTotal,
                invoice.Discount,
                invoice.TaxableAmount,
                invoice.CgstAmount,
                invoice.SgstAmount,
                invoice.IgstAmount,
                invoice.TotalAmount,
                invoice.AmountPaid,
                invoice.Balance,
                invoice.PaymentStatus,
                invoice.Notes,
                invoice.SupplyType,
                invoice.PlaceOfSupplyStateCode,
                invoice.PlaceOfSupplyStateName,
                invoice.CustomerGstin);
        }).ToList();

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        return new PagedResult<SalesInvoiceSummaryDto>(items, page, pageSize, totalCount, totalPages);
    }
}

public sealed record GetSalesInvoiceDetailsQuery(Guid Id) : IRequest<SalesInvoiceDetailsDto>;

public sealed class GetSalesInvoiceDetailsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesInvoiceDetailsQuery, SalesInvoiceDetailsDto>
{
    public async Task<SalesInvoiceDetailsDto> Handle(GetSalesInvoiceDetailsQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .Include(i => i.Lines)
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Sales invoice '{request.Id}' was not found.");

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == invoice.CustomerId, cancellationToken);

        var receipts = await db.SalesInvoiceReceipts.AsNoTracking()
            .Where(r => r.InvoiceId == invoice.Id)
            .OrderBy(r => r.PaymentDate)
            .Select(r => new SalesInvoiceReceiptDto(r.Id, r.Amount, r.PaymentMode, r.PaymentDate, r.ReferenceNumber, r.Note))
            .ToListAsync(cancellationToken);

        var summary = new SalesInvoiceSummaryDto(
            invoice.Id,
            invoice.BillNumber,
            invoice.CustomerId,
            customer?.Name ?? "Unknown",
            customer?.Mobile ?? "",
            customer?.Address ?? "",
            customer?.Email,
            invoice.InvoiceDate,
            invoice.PaymentTermsDays,
            invoice.DueDate,
            invoice.Lines.Count,
            invoice.SubTotal,
            invoice.Discount,
            invoice.TaxableAmount,
            invoice.CgstAmount,
            invoice.SgstAmount,
            invoice.IgstAmount,
            invoice.TotalAmount,
            invoice.AmountPaid,
            invoice.Balance,
            invoice.PaymentStatus,
            invoice.Notes,
            invoice.SupplyType,
            invoice.PlaceOfSupplyStateCode,
            invoice.PlaceOfSupplyStateName,
            invoice.CustomerGstin);

        var lines = invoice.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesInvoiceLineDto(
            l.Id,
            l.LineNumber,
            l.ItemType,
            l.ProductId,
            l.ItemDescription,
            l.SerialNumber,
            l.SerialNumber1,
            l.Quantity,
            l.UnitPrice,
            l.Discount,
            l.TaxId,
            l.CgstRate,
            l.SgstRate,
            l.IgstRate,
            l.TaxableAmount,
            l.CgstAmount,
            l.SgstAmount,
            l.IgstAmount,
            l.TotalAmount, l.HsnSac, l.UnitOfMeasure, l.SkuId)).ToList();

        return new SalesInvoiceDetailsDto(summary, lines, receipts);
    }
}

