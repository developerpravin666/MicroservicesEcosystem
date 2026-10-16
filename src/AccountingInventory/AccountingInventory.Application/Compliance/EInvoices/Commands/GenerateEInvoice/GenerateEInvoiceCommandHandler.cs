using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.GenerateEInvoice;

public sealed class GenerateEInvoiceCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GenerateEInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(GenerateEInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await db.SalesInvoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == request.SalesInvoiceId, ct)
            ?? throw new NotFoundException($"Sales Invoice with ID '{request.SalesInvoiceId}' was not found.");

        if (invoice.IsCancelled)
            throw new ConflictException("Cannot generate an E-Invoice for a cancelled invoice.");

        var existingActive = await db.EInvoices
            .FirstOrDefaultAsync(e => e.SalesInvoiceId == invoice.Id && e.Status == "Generated", ct);
        if (existingActive != null)
            throw new ConflictException($"An active E-Invoice (IRN: {existingActive.Irn}) already exists for this invoice. Cancel it first before generating a new one.");

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == invoice.CustomerId, ct)
            ?? throw new NotFoundException("Customer associated with invoice not found.");

        var seller = await db.CustomerBillSettings.FirstOrDefaultAsync(ct)
            ?? CustomerBillSettings.Create("My Company", "Head Office", "9999999999", null, null, "TAX INVOICE", "Thank you", "A4", true, true, true, true, true);

        var sellerGstin = seller.TaxRegistrationNumber ?? "29AABCU9603R1ZJ";
        var buyerGstin = !string.IsNullOrWhiteSpace(invoice.CustomerGstin)
            ? invoice.CustomerGstin
            : (!string.IsNullOrWhiteSpace(customer.Gstin) ? customer.Gstin : null);

        var irn = !string.IsNullOrWhiteSpace(request.CustomIrn)
            ? request.CustomIrn.Trim()
            : EInvoiceHelper.ComputeIrn(sellerGstin, invoice.InvoiceDate, "INV", invoice.BillNumber);

        var ackNo = !string.IsNullOrWhiteSpace(request.CustomAckNo)
            ? request.CustomAckNo.Trim()
            : $"{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000000, 999999999)}";

        var ackDate = DateTimeOffset.UtcNow;

        var mainHsn = invoice.Lines.FirstOrDefault()?.HsnSac ?? "85171200";

        var qrCodePayload = EInvoiceHelper.GenerateSignedQrPayload(
            sellerGstin,
            buyerGstin,
            invoice.BillNumber,
            invoice.InvoiceDate,
            invoice.TotalAmount,
            invoice.Lines.Count,
            mainHsn,
            irn);

        var payloadJson = EInvoiceHelper.GenerateEInvoiceJson(invoice, seller, customer);

        var eInvoice = EInvoice.Create(
            invoice.Id,
            irn,
            ackNo,
            ackDate,
            qrCodePayload,
            null,
            payloadJson);

        db.EInvoices.Add(eInvoice);
        await db.SaveChangesAsync(ct);

        return eInvoice.Id;
    }
}

