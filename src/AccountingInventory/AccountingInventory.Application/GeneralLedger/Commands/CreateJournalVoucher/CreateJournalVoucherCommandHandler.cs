using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Commands.CreateJournalVoucher;

public sealed class CreateJournalVoucherCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateJournalVoucherCommand, JournalSummary>
{
    public async Task<JournalSummary> Handle(CreateJournalVoucherCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.VoucherDate, ct);

        var ledgerIds = request.Lines.Select(l => l.LedgerId).Distinct().ToArray();
        var accounts = await db.ChartAccounts.AsNoTracking()
            .Where(a => ledgerIds.Contains(a.Id) && a.IsActive)
            .ToDictionaryAsync(a => a.Id, ct);

        if (accounts.Count != ledgerIds.Length)
            throw new ConflictException("Every line must reference an active ledger account.");

        if (request.ApprovalId is null && accounts.Values.Any(a => a.Code is "1100" or "1200" or "2000"))
            throw new ConflictException("Receivables (1100), inventory (1200), and payables (2000) require their source billing/payment workflow or an approved financial correction.");

        var dimensionIds = request.Lines.Where(l => l.DimensionId.HasValue).Select(l => l.DimensionId!.Value).Distinct().ToArray();
        if (dimensionIds.Length > 0)
        {
            var validDimensions = await db.AccountingDimensions.AsNoTracking()
                .Where(x => dimensionIds.Contains(x.Id) && x.IsActive)
                .Select(x => x.Id).ToListAsync(ct);
            if (validDimensions.Count != dimensionIds.Length)
                throw new ConflictException("Every specified dimension / cost center must be active.");
        }

        var sourceId = $"JRN-{Guid.NewGuid():N}";
        var roundedLines = request.Lines.Select(l => (
            AccountId: l.LedgerId,
            Debit: decimal.Round(l.DebitAmount, 2, MidpointRounding.AwayFromZero),
            Credit: decimal.Round(l.CreditAmount, 2, MidpointRounding.AwayFromZero),
            Memo: l.LineNarration
        )).ToArray();

        var refPart = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? "" : $" [{request.ReferenceNumber.Trim()}]";
        var description = $"{request.Narration.Trim()}{refPart}";
        if (description.Length > 500) description = description[..500];

        var entry = JournalEntry.Post(request.VoucherDate, description, "Journal", sourceId, roundedLines);

        var lineIndex = 0;
        foreach (var entryLine in entry.Lines)
        {
            var inputLine = request.Lines.ElementAt(lineIndex++);
            entryLine.AssignDimension(inputLine.DimensionId);
        }

        db.JournalEntries.Add(entry);
        db.JournalLines.AddRange(entry.Lines);
        await db.SaveChangesAsync(ct);

        var rows = entry.Lines.Select(line => new JournalLineSummary(
            line.AccountId,
            accounts[line.AccountId].Code,
            accounts[line.AccountId].Name,
            line.Debit,
            line.Credit,
            line.Memo,
            line.DimensionId
        )).ToArray();

        return new JournalSummary(
            entry.Id,
            entry.JournalNumber,
            entry.JournalDate,
            entry.Description,
            entry.SourceType,
            entry.SourceId,
            entry.PostedAt,
            entry.ReversalOfJournalEntryId,
            false,
            rows,
            rows.Sum(r => r.Debit),
            rows.Sum(r => r.Credit));
    }
}

