using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Compliance.EWayBills.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillByInvoiceId;

public sealed class GetEWayBillByInvoiceIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEWayBillByInvoiceIdQuery, EWayBillDto?>
{
    public async Task<EWayBillDto?> Handle(GetEWayBillByInvoiceIdQuery request, CancellationToken ct)
    {
        var e = await db.EWayBills.AsNoTracking()
            .Where(x => x.SalesInvoiceId == request.SalesInvoiceId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (e == null) return null;

        var inv = await db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == e.SalesInvoiceId, ct);

        string customerName = "Unknown";
        string? customerGstin = inv?.CustomerGstin;

        if (inv != null)
        {
            var cust = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == inv.CustomerId, ct);
            if (cust != null)
            {
                customerName = cust.Name;
                customerGstin ??= cust.Gstin;
            }
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
    }
}

