using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.Rcm.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoices;

public sealed record GetRcmSelfInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Section = null,
    string? Status = null,
    string? Search = null) : IRequest<PagedResult<RcmSelfInvoiceDto>>;

