using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.GeneralLedger.Commands.CreateContraVoucher;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetContraVouchers;

public sealed class GetContraVouchersQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetContraVouchersQuery, PagedResult<ContraVoucherSummary>>
{
    public async Task<PagedResult<ContraVoucherSummary>> Handle(GetContraVouchersQuery request, CancellationToken ct)
    {
        var query = db.JournalEntries.AsNoTracking()
            .Where(e => e.SourceType == "Contra")
            .OrderByDescending(e => e.JournalDate)
            .ThenByDescending(e => e.PostedAt);

        var count = await query.CountAsync(ct);
        var page = Math.Max(request.Page, 1);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var entries = await query.Skip((page - 1) * size).Take(size)
            .Select(e => new { e.Id, e.JournalNumber, e.JournalDate, e.Description, e.SourceId, e.PostedAt, e.ReversalOfJournalEntryId })
            .ToListAsync(ct);

        var entryIds = entries.Select(e => e.Id).ToArray();
        var reversedIds = await db.JournalEntries.AsNoTracking()
            .Where(e => e.ReversalOfJournalEntryId.HasValue && entryIds.Contains(e.ReversalOfJournalEntryId.Value))
            .Select(e => e.ReversalOfJournalEntryId!.Value)
            .ToListAsync(ct);
        var reversedSet = reversedIds.ToHashSet();

        var lines = await (from line in db.JournalLines.AsNoTracking()
            join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
            where entryIds.Contains(line.JournalEntryId)
            select new { line.JournalEntryId, line.AccountId, account.Code, account.Name, line.Debit, line.Credit, line.Memo })
            .ToListAsync(ct);

        var results = entries.Select(entry =>
        {
            var entryLines = lines.Where(l => l.JournalEntryId == entry.Id).ToList();
            var toLine = entryLines.FirstOrDefault(l => l.Debit > 0);
            var fromLine = entryLines.FirstOrDefault(l => l.Credit > 0);

            var amount = toLine?.Debit ?? fromLine?.Credit ?? 0m;
            return new ContraVoucherSummary(
                entry.Id,
                entry.JournalNumber,
                entry.JournalDate,
                fromLine?.AccountId ?? Guid.Empty,
                fromLine?.Name ?? "Unknown Account",
                fromLine?.Code ?? "-",
                toLine?.AccountId ?? Guid.Empty,
                toLine?.Name ?? "Unknown Account",
                toLine?.Code ?? "-",
                amount,
                null,
                null,
                null,
                entry.Description,
                reversedSet.Contains(entry.Id),
                entry.ReversalOfJournalEntryId,
                entry.PostedAt);
        }).ToArray();

        return new PagedResult<ContraVoucherSummary>(results, page, size, count,
            count == 0 ? 0 : (int)Math.Ceiling(count / (double)size));
    }
}

