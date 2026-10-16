using AccountingInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger.Common;

public static class BankReconciliationMapper
{
    public static async Task<BankReconciliationSummary> MapAsync(IAccountingInventoryDbContext db, Guid id, CancellationToken ct)
    {
        var item = await db.BankReconciliations.AsNoTracking().SingleAsync(row => row.Id == id, ct);
        var account = await db.ChartAccounts.AsNoTracking().SingleAsync(row => row.Id == item.AccountId, ct);
        var lines = await db.BankStatementLines.AsNoTracking().Where(line => line.BankReconciliationId == item.Id)
            .OrderBy(line => line.TransactionDate)
            .Select(line => new BankReconciliationLineSummary(line.Id, line.TransactionDate, line.Description,
                line.Reference, line.Amount, line.JournalLineId)).ToListAsync(ct);
        return new BankReconciliationSummary(item.Id, item.AccountId, account.Code, account.Name, item.StatementReference,
            item.StartDate, item.EndDate, item.OpeningBalance, item.ClosingBalance, item.IsFinalized, lines);
    }
}

