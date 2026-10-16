using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.Rcm.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoices;

public sealed class GetRcmSelfInvoicesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetRcmSelfInvoicesQuery, PagedResult<RcmSelfInvoiceDto>>
{
    public async Task<PagedResult<RcmSelfInvoiceDto>> Handle(GetRcmSelfInvoicesQuery request, CancellationToken ct)
    {
        var query = db.RcmSelfInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(r => r.Status == request.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Section))
        {
            query = query.Where(r => r.Section == request.Section.Trim());
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(r => r.VoucherDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(r => r.VoucherDate <= request.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(r =>
                r.VoucherNumber.ToLower().Contains(search)
                || r.PaymentVoucherNumber.ToLower().Contains(search)
                || r.VendorName.ToLower().Contains(search)
                || r.ItemDescription.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var list = await query
            .OrderByDescending(r => r.VoucherDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = list.Select(r => new RcmSelfInvoiceDto(
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
            r.Notes)).ToList();

        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        return new PagedResult<RcmSelfInvoiceDto>(items, page, pageSize, totalCount, totalPages);
    }
}

