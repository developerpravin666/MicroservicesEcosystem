using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.EWayBills.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBills;

public sealed class GetEWayBillsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEWayBillsQuery, PagedResult<EWayBillDto>>
{
    public async Task<PagedResult<EWayBillDto>> Handle(GetEWayBillsQuery request, CancellationToken ct)
    {
        var ewbQuery = db.EWayBills.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            ewbQuery = ewbQuery.Where(e => e.Status == request.Status.Trim());
        }

        if (request.FromDate.HasValue)
        {
            var fromDto = new DateTimeOffset(request.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            ewbQuery = ewbQuery.Where(e => e.EwbDate >= fromDto);
        }

        if (request.ToDate.HasValue)
        {
            var toDto = new DateTimeOffset(request.ToDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            ewbQuery = ewbQuery.Where(e => e.EwbDate <= toDto);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            ewbQuery = ewbQuery.Where(e =>
                (e.EwbNumber != null && e.EwbNumber.ToLower().Contains(search))
                || (e.VehicleNo != null && e.VehicleNo.ToLower().Contains(search))
                || (e.TransporterName != null && e.TransporterName.ToLower().Contains(search)));
        }

        var totalCount = await ewbQuery.CountAsync(ct);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var ewbList = await ewbQuery
            .OrderByDescending(e => e.EwbDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var invoiceIds = ewbList.Select(e => e.SalesInvoiceId).Distinct().ToList();
        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var customerIds = invoices.Values.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var items = ewbList.Select(e =>
        {
            invoices.TryGetValue(e.SalesInvoiceId, out var inv);
            string customerName = "Unknown";
            string? customerGstin = inv?.CustomerGstin;

            if (inv != null && customers.TryGetValue(inv.CustomerId, out var cust))
            {
                customerName = cust.Name;
                customerGstin ??= cust.Gstin;
            }

            return new EWayBillDto(
                e.Id,
                e.SalesInvoiceId,
                inv?.BillNumber ?? "Unknown",
                inv?.InvoiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                customerName,
                customerGstin,
                inv?.TotalAmount ?? 0m,
                e.EwbNumber,
                e.EwbDate,
                e.ValidUpto,
                e.DocType,
                e.SupplyType,
                e.SubSupplyType,
                e.TransMode,
                e.DistanceKm,
                e.TransporterId,
                e.TransporterName,
                e.TransDocNo,
                e.TransDocDate,
                e.VehicleNo,
                e.VehicleType,
                e.Status,
                e.CancelReason,
                e.CancelDate);
        }).ToList();

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        return new PagedResult<EWayBillDto>(items, page, pageSize, totalCount, totalPages);
    }
}

