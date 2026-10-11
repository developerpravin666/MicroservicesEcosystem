using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record CashFlowLine(string Code, string Account, decimal Amount);
public sealed record CashFlowStatementSummary(DateOnly FromDate, DateOnly ToDate,
    IReadOnlyList<CashFlowLine> Operating, decimal OperatingTotal,
    IReadOnlyList<CashFlowLine> Investing, decimal InvestingTotal,
    IReadOnlyList<CashFlowLine> Financing, decimal FinancingTotal,
    decimal OpeningCash, decimal NetChange, decimal ClosingCash);
public sealed record GetCashFlowStatementQuery(DateOnly? FromDate, DateOnly? ToDate)
    : IRequest<CashFlowStatementSummary>;

/// <summary>Direct cash flow view calculated from posted cash/bank journal lines.</summary>
public sealed class GetCashFlowStatementHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCashFlowStatementQuery, CashFlowStatementSummary>
{
    public async Task<CashFlowStatementSummary> Handle(GetCashFlowStatementQuery request, CancellationToken ct)
    {
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? new DateOnly(to.Year, 1, 1);
        if (fromDate > to) throw new ArgumentException("From date must be on or before to date.");

        var cashAccounts = await db.ChartAccounts.AsNoTracking()
            .Where(account => account.Code == "1000" || account.Code == "1010")
            .Select(account => account.Id).ToListAsync(ct);
        if (cashAccounts.Count == 0)
            return new(fromDate, to, [], 0, [], 0, [], 0, 0, 0, 0);

        var lines = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
            where entry.JournalDate >= fromDate && entry.JournalDate <= to
            select new { entry.JournalDate, line.JournalEntryId, line.AccountId, account.Code,
                account.Name, account.Type, line.Debit, line.Credit }).ToListAsync(ct);

        var entries = lines.GroupBy(line => line.JournalEntryId);
        var classified = new List<(string Category, string Code, string Name, decimal Amount)>();
        foreach (var entry in entries)
        {
            var cash = entry.Where(line => cashAccounts.Contains(line.AccountId)).Sum(line => line.Debit - line.Credit);
            if (cash == 0) continue;
            // Transfers between cash accounts have no external cash flow.
            var counterparts = entry.Where(line => !cashAccounts.Contains(line.AccountId)).ToArray();
            foreach (var line in counterparts)
            {
                var category = Classify(line.Code, line.Type);
                var amount = line.Credit - line.Debit;
                if (amount != 0) classified.Add((category, line.Code, line.Name, amount));
            }
        }

        IReadOnlyList<CashFlowLine> Summarize(string category)
            => classified.Where(line => line.Category == category).GroupBy(line => new { line.Code, line.Name })
                .Select(group => new CashFlowLine(group.Key.Code, group.Key.Name, group.Sum(line => line.Amount)))
                .Where(line => line.Amount != 0).OrderBy(line => line.Code).ToArray();
        var operating = Summarize("Operating");
        var investing = Summarize("Investing");
        var financing = Summarize("Financing");
        var opening = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where cashAccounts.Contains(line.AccountId) && entry.JournalDate < fromDate
            select line.Debit - line.Credit).SumAsync(ct);
        var net = operating.Sum(line => line.Amount) + investing.Sum(line => line.Amount) + financing.Sum(line => line.Amount);
        return new(fromDate, to, operating, operating.Sum(line => line.Amount), investing,
            investing.Sum(line => line.Amount), financing, financing.Sum(line => line.Amount), opening, net, opening + net);
    }

    private static string Classify(string code, LedgerAccountType type)
    {
        if (type == LedgerAccountType.Equity || code.StartsWith("23", StringComparison.Ordinal)
            || code.StartsWith("24", StringComparison.Ordinal)) return "Financing";
        if (type == LedgerAccountType.Asset && (code.StartsWith("13", StringComparison.Ordinal)
            || code.StartsWith("14", StringComparison.Ordinal) || code.StartsWith("15", StringComparison.Ordinal))) return "Investing";
        return "Operating";
    }
}
