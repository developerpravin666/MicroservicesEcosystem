using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Compliance.Rcm.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoiceById;

public sealed class GetRcmSelfInvoiceByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetRcmSelfInvoiceByIdQuery, RcmSelfInvoiceDto>
{
    public async Task<RcmSelfInvoiceDto> Handle(GetRcmSelfInvoiceByIdQuery request, CancellationToken ct)
    {
        var r = await db.RcmSelfInvoices.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException($"RCM Self-Invoice with ID '{request.Id}' was not found.");

        return new RcmSelfInvoiceDto(
            r.Id,
            r.VoucherNumber,
            r.PaymentVoucherNumber,
            r.VoucherDate,
            r.VendorId,
            r.VendorName,
            r.VendorGstin,
            r.Section,
            r.ServiceCategory,
            r.SupplierInvoiceNumber,
            r.SupplierInvoiceDate,
            r.SupplyType,
            r.PlaceOfSupplyStateCode,
            r.PlaceOfSupplyStateName,
            r.HsnSacCode,
            r.ItemDescription,
            r.TaxableAmount,
            r.GstRate,
            r.CgstRate,
            r.SgstRate,
            r.IgstRate,
            r.CgstAmount,
            r.SgstAmount,
            r.IgstAmount,
            r.TotalTaxAmount,
            r.TotalAmount,
            r.IsItcEligible,
            r.ItcClaimedDate,
            r.PaymentMode,
            r.PaymentReference,
            r.BankAccountId,
            r.JournalEntryId,
            r.Status,
            r.CancelReason,
            r.CancelDate,
            r.Notes);
    }
}

