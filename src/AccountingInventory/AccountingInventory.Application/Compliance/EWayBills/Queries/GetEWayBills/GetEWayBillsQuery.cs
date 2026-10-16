using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Compliance.EWayBills.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBills;

public sealed record GetEWayBillsQuery(
    int Page = 1,
    int PageSize = 20,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Status = null,
    string? Search = null) : IRequest<PagedResult<EWayBillDto>>;

