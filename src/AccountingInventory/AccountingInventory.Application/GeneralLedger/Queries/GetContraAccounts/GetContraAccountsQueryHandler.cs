using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetContraAccounts;

public sealed class GetContraAccountsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetContraAccountsQuery, IReadOnlyList<ContraAccountSummary>>
{
    public async Task<IReadOnlyList<ContraAccountSummary>> Handle(GetContraAccountsQuery request, CancellationToken ct)
    {
        var accounts = await db.ChartAccounts.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        var eligibleAccounts = accounts.Where(ContraHelper.IsCashOrBankAccount).ToList();
        var eligibleIds = eligibleAccounts.Select(a => a.Id).ToHashSet();

        var balances = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where eligibleIds.Contains(line.AccountId)
            group line by line.AccountId into g
            select new { AccountId = g.Key, Debit = g.Sum(l => l.Debit), Credit = g.Sum(l => l.Credit) })
            .ToDictionaryAsync(r => r.AccountId, ct);

        return eligibleAccounts.Select(a =>
        {
            var net = balances.TryGetValue(a.Id, out var b) ? b.Debit - b.Credit : 0m;
            return new ContraAccountSummary(a.Id, a.Code, a.Name, a.Type, net);
        }).ToArray();
    }
}

