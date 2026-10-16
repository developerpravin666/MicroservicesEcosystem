using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.GeneralLedger.Commands.CreateContraVoucher;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetContraVouchers;

public sealed record GetContraVouchersQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ContraVoucherSummary>>;

