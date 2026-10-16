using AccountingInventory.Application.GeneralLedger;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Commands.AcceptBankStatementSuggestion;

public sealed record AcceptBankStatementSuggestionCommand(
    Guid ReconciliationId,
    Guid StatementLineId,
    string SuggestionType,
    Guid EntityId,
    string? BillOrReferenceNumber = null) : IRequest<BankReconciliationSummary>;

