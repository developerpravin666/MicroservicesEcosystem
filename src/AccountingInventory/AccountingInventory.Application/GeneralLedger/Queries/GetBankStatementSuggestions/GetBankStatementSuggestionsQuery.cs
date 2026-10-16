using AccountingInventory.Application.GeneralLedger.Common;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetBankStatementSuggestions;

public sealed record GetBankStatementSuggestionsQuery(Guid ReconciliationId)
    : IRequest<IReadOnlyList<BankStatementSuggestionDto>>;

