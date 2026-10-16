using AccountingInventory.Domain.Entities;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetContraAccounts;

public sealed record ContraAccountSummary(
    Guid Id,
    string Code,
    string Name,
    LedgerAccountType Type,
    decimal CurrentBalance);

public sealed record GetContraAccountsQuery() : IRequest<IReadOnlyList<ContraAccountSummary>>;

