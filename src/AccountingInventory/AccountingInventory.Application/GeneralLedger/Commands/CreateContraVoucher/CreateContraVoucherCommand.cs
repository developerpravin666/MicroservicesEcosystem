using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateContraVoucher;

public sealed record ContraVoucherSummary(
    Guid Id,
    string VoucherNumber,
    DateOnly VoucherDate,
    Guid FromLedgerId,
    string FromLedgerName,
    string FromLedgerCode,
    Guid ToLedgerId,
    string ToLedgerName,
    string ToLedgerCode,
    decimal Amount,
    string? ReferenceNumber,
    string? InstrumentType,
    DateOnly? ClearingDate,
    string Narration,
    bool IsReversed,
    Guid? ReversalOfJournalEntryId,
    DateTimeOffset PostedAt);

public sealed record CreateContraVoucherCommand(
    DateOnly VoucherDate,
    Guid FromLedgerId,
    Guid ToLedgerId,
    decimal Amount,
    string? ReferenceNumber = null,
    string? Narration = null,
    string? InstrumentType = null,
    DateOnly? ClearingDate = null) : IRequest<ContraVoucherSummary>;

