using AccountingInventory.Application.GeneralLedger;
using MediatR;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateJournalVoucher;

public sealed record JournalVoucherLineInput(
    Guid LedgerId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? LineNarration = null,
    Guid? DimensionId = null);

public sealed record CreateJournalVoucherCommand(
    DateOnly VoucherDate,
    string? ReferenceNumber,
    string Narration,
    IReadOnlyCollection<JournalVoucherLineInput> Lines,
    Guid? ApprovalId = null) : IRequest<JournalSummary>;

