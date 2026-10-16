using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger.Common;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Commands.AutoMatchBankReconciliation;

public sealed class AutoMatchBankReconciliationCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<AutoMatchBankReconciliationCommand, AutoMatchResultDto>
{
    public async Task<AutoMatchResultDto> Handle(AutoMatchBankReconciliationCommand request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations
            .SingleOrDefaultAsync(r => r.Id == request.ReconciliationId, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");

        if (reconciliation.IsFinalized)
            throw new ConflictException("Finalized reconciliations cannot be modified.");

        var statementLines = await db.BankStatementLines
            .Where(line => line.BankReconciliationId == reconciliation.Id && line.JournalLineId == null)
            .ToListAsync(ct);

        if (statementLines.Count == 0)
        {
            return new AutoMatchResultDto(0, 0m, ["No unmatched statement transactions found."]);
        }

        // Fetch candidate unmatched journal lines within a +/- 2 day window of the statement period
        var minDate = reconciliation.StartDate.AddDays(-2);
        var maxDate = reconciliation.EndDate.AddDays(2);

        var ledgerCandidates = await (from line in db.JournalLines
            join entry in db.JournalEntries on line.JournalEntryId equals entry.Id
            where line.AccountId == reconciliation.AccountId
                && entry.JournalDate >= minDate
                && entry.JournalDate <= maxDate
                && !db.BankStatementLines.Any(match => match.JournalLineId == line.Id)
            select new LedgerCandidate
            {
                JournalLineId = line.Id,
                JournalDate = entry.JournalDate,
                JournalNumber = entry.JournalNumber,
                Description = entry.Description,
                Memo = line.Memo,
                Amount = decimal.Round(line.Debit - line.Credit, 2, MidpointRounding.AwayFromZero)
            }).ToListAsync(ct);

        var matchDetails = new List<string>();
        int matchedCount = 0;
        decimal totalMatchedAmount = 0m;
        var matchedJournalLineIds = new HashSet<Guid>();

        // RULE 1: Match by exact Cheque / UTR / IMPS reference number & exact amount
        foreach (var stmt in statementLines)
        {
            if (stmt.JournalLineId != null) continue;
            if (string.IsNullOrWhiteSpace(stmt.Reference)) continue;

            var cleanRef = stmt.Reference.Trim();
            var matches = ledgerCandidates
                .Where(c => !matchedJournalLineIds.Contains(c.JournalLineId)
                    && c.Amount == stmt.Amount
                    && (c.JournalNumber.Contains(cleanRef, StringComparison.OrdinalIgnoreCase)
                        || (c.Memo != null && c.Memo.Contains(cleanRef, StringComparison.OrdinalIgnoreCase))
                        || c.Description.Contains(cleanRef, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count == 1)
            {
                var candidate = matches[0];
                stmt.Match(candidate.JournalLineId);
                matchedJournalLineIds.Add(candidate.JournalLineId);
                matchedCount++;
                totalMatchedAmount += Math.Abs(stmt.Amount);
                matchDetails.Add($"[Ref Match] Stmt Ref '{cleanRef}' ({stmt.Amount:N2}) matched with Journal {candidate.JournalNumber} ({candidate.JournalDate:yyyy-MM-dd})");
            }
        }

        // RULE 2: Match by exact amount and date window (+/- 2 days)
        foreach (var stmt in statementLines)
        {
            if (stmt.JournalLineId != null) continue;

            var candidates = ledgerCandidates
                .Where(c => !matchedJournalLineIds.Contains(c.JournalLineId)
                    && c.Amount == stmt.Amount
                    && Math.Abs(c.JournalDate.DayNumber - stmt.TransactionDate.DayNumber) <= 2)
                .ToList();

            // Match only when there is an unambiguous 1:1 match
            if (candidates.Count == 1)
            {
                var candidate = candidates[0];
                stmt.Match(candidate.JournalLineId);
                matchedJournalLineIds.Add(candidate.JournalLineId);
                matchedCount++;
                totalMatchedAmount += Math.Abs(stmt.Amount);
                matchDetails.Add($"[Amount & Date Window (+/-2d)] Stmt {stmt.TransactionDate:yyyy-MM-dd} ({stmt.Amount:N2}) matched with Journal {candidate.JournalNumber} ({candidate.JournalDate:yyyy-MM-dd})");
            }
        }

        if (matchedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new AutoMatchResultDto(matchedCount, totalMatchedAmount, matchDetails);
    }

    private sealed class LedgerCandidate
    {
        public Guid JournalLineId { get; set; }
        public DateOnly JournalDate { get; set; }
        public string JournalNumber { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Memo { get; set; }
        public decimal Amount { get; set; }
    }
}

