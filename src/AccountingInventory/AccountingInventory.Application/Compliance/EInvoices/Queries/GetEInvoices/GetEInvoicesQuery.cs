using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.EInvoices.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoices;

public sealed record GetEInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Status = null,
    string? Search = null) : IRequest<PagedResult<EInvoiceDto>>;

