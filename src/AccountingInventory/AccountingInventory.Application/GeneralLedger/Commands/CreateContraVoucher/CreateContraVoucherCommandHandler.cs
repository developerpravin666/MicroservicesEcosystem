using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateContraVoucher;

public sealed class CreateContraVoucherCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateContraVoucherCommand, ContraVoucherSummary>
{
    public async Task<ContraVoucherSummary> Handle(CreateContraVoucherCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.VoucherDate, ct);

        var fromAccount = await db.ChartAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.FromLedgerId && a.IsActive, ct)
            ?? throw new NotFoundException("Source ledger account was not found or is inactive.");
        var toAccount = await db.ChartAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ToLedgerId && a.IsActive, ct)
            ?? throw new NotFoundException("Destination ledger account was not found or is inactive.");

        if (!ContraHelper.IsCashOrBankAccount(fromAccount))
            throw new ConflictException($"Account '{fromAccount.Name}' ({fromAccount.Code}) is not a Cash or Bank account.");
        if (!ContraHelper.IsCashOrBankAccount(toAccount))
            throw new ConflictException($"Account '{toAccount.Name}' ({toAccount.Code}) is not a Cash or Bank account.");

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) throw new ConflictException("Amount must be greater than zero.");

        var refPart = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? string.Empty : $" Ref: {request.ReferenceNumber.Trim()}";
        var instPart = string.IsNullOrWhiteSpace(request.InstrumentType) ? string.Empty : $" [{request.InstrumentType.Trim()}]";
        var narration = string.IsNullOrWhiteSpace(request.Narration)
            ? $"Contra transfer from {fromAccount.Name} to {toAccount.Name}{instPart}{refPart}"
            : request.Narration.Trim();

        var toMemo = $"Contra receipt from {fromAccount.Name}{instPart}{refPart}".Trim();
        var fromMemo = $"Contra payment to {toAccount.Name}{instPart}{refPart}".Trim();
        if (request.ClearingDate.HasValue)
        {
            toMemo += $" (Clearing: {request.ClearingDate.Value:yyyy-MM-dd})";
            fromMemo += $" (Clearing: {request.ClearingDate.Value:yyyy-MM-dd})";
        }

        var sourceId = $"CTR-{Guid.NewGuid():N}";

        var lines = new (Guid AccountId, decimal Debit, decimal Credit, string? Memo)[]
        {
            (request.ToLedgerId, amount, 0m, toMemo.Length > 250 ? toMemo[..250] : toMemo),
            (request.FromLedgerId, 0m, amount, fromMemo.Length > 250 ? fromMemo[..250] : fromMemo)
        };

        var entry = JournalEntry.Post(request.VoucherDate, narration.Length > 500 ? narration[..500] : narration,
            "Contra", sourceId, lines);

        db.JournalEntries.Add(entry);
        db.JournalLines.AddRange(entry.Lines);
        await db.SaveChangesAsync(ct);

        return new ContraVoucherSummary(
            entry.Id,
            entry.JournalNumber,
            entry.JournalDate,
            fromAccount.Id,
            fromAccount.Name,
            fromAccount.Code,
            toAccount.Id,
            toAccount.Name,
            toAccount.Code,
            amount,
            request.ReferenceNumber?.Trim(),
            request.InstrumentType?.Trim(),
            request.ClearingDate,
            entry.Description,
            false,
            null,
            entry.PostedAt);
    }
}

