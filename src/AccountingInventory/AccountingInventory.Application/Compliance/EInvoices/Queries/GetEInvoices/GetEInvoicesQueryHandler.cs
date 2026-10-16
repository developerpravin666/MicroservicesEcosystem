using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.EInvoices.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoices;

public sealed class GetEInvoicesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEInvoicesQuery, PagedResult<EInvoiceDto>>
{
    public async Task<PagedResult<EInvoiceDto>> Handle(GetEInvoicesQuery request, CancellationToken ct)
    {
        var query = db.EInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(e => e.Status == request.Status.Trim());
        }

        if (request.FromDate.HasValue)
        {
            var fromDto = new DateTimeOffset(request.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.AckDate >= fromDto);
        }

        if (request.ToDate.HasValue)
        {
            var toDto = new DateTimeOffset(request.ToDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(e => e.AckDate <= toDto);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(e =>
                e.Irn.ToLower().Contains(search)
                || e.AckNo.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var list = await query
            .OrderByDescending(e => e.AckDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var invoiceIds = list.Select(e => e.SalesInvoiceId).Distinct().ToList();
        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var customerIds = invoices.Values.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var items = list.Select(e =>
        {
            invoices.TryGetValue(e.SalesInvoiceId, out var inv);
            string customerName = "Unknown";
            string? customerGstin = inv?.CustomerGstin;

            if (inv != null && customers.TryGetValue(inv.CustomerId, out var cust))
            {
                customerName = cust.Name;
                customerGstin ??= cust.Gstin;
            }

            return new EInvoiceDto(
                e.Id,
                e.SalesInvoiceId,
                inv?.BillNumber ?? "Unknown",
                inv?.InvoiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                customerName,
                customerGstin,
                inv?.TotalAmount ?? 0m,
                e.Irn,
                e.AckNo,
                e.AckDate,
                e.SignedQrCode,
                e.SignedInvoice,
                e.Status,
                e.CancelReason,
                e.CancelDate);
        }).ToList();

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        return new PagedResult<EInvoiceDto>(items, page, pageSize, totalCount, totalPages);
    }
}

