namespace AccountingInventory.Application.GeneralLedger.Common;

public sealed record BankStatementSuggestionDto(
    Guid StatementLineId,
    DateOnly TransactionDate,
    decimal StatementAmount,
    string? StatementDescription,
    string? StatementReference,
    string SuggestionType, // "CustomerReceipt" or "VendorPayment"
    Guid EntityId,
    string PartyName,
    string DocumentNumber,
    decimal DocumentAmount,
    decimal DocumentBalance,
    string MatchConfidence, // "High" or "Medium"
    string MatchReason);

