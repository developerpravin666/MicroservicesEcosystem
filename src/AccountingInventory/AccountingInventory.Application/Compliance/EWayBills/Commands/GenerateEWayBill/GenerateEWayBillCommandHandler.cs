using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.GenerateEWayBill;

public sealed class GenerateEWayBillCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GenerateEWayBillCommand, Guid>
{
    public async Task<Guid> Handle(GenerateEWayBillCommand request, CancellationToken ct)
    {
        var invoice = await db.SalesInvoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == request.SalesInvoiceId, ct)
            ?? throw new NotFoundException($"Sales Invoice with ID '{request.SalesInvoiceId}' was not found.");

        if (invoice.IsCancelled)
            throw new ConflictException("Cannot generate an E-Way Bill for a cancelled invoice.");

        var existingActive = await db.EWayBills
            .FirstOrDefaultAsync(e => e.SalesInvoiceId == invoice.Id && e.Status == "Generated", ct);
        if (existingActive != null)
            throw new ConflictException($"An active E-Way Bill ({existingActive.EwbNumber}) already exists for this invoice. Cancel it first before generating a new one.");

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == invoice.CustomerId, ct)
            ?? throw new NotFoundException("Customer associated with invoice not found.");

        var seller = await db.CustomerBillSettings.FirstOrDefaultAsync(ct)
            ?? CustomerBillSettings.Create("My Company", "Head Office", "9999999999", null, null, "TAX INVOICE", "Thank you", "A4", true, true, true, true, true);

        var ewb = EWayBill.Create(
            invoice.Id,
            request.DistanceKm,
            request.TransMode,
            request.VehicleNo,
            request.VehicleType,
            request.TransporterId,
            request.TransporterName,
            request.TransDocNo,
            request.TransDocDate,
            request.DocType,
            request.SupplyType,
            request.SubSupplyType,
            request.CustomEwbNumber);

        var payloadJson = EWayBillHelper.GenerateEWayBillJson(invoice, seller, customer, ewb);
        ewb.SetPayloadJson(payloadJson);

        db.EWayBills.Add(ewb);
        await db.SaveChangesAsync(ct);

        return ewb.Id;
    }
}

