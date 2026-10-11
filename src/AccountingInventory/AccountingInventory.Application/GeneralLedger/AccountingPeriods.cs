using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record AccountingPeriodSummary(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate,
    bool IsClosed, DateTimeOffset? ClosedAt);

public sealed record GetAccountingPeriodsQuery() : IRequest<IReadOnlyList<AccountingPeriodSummary>>;

public sealed class GetAccountingPeriodsHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetAccountingPeriodsQuery, IReadOnlyList<AccountingPeriodSummary>>
{
    public async Task<IReadOnlyList<AccountingPeriodSummary>> Handle(GetAccountingPeriodsQuery request, CancellationToken ct)
        => await db.AccountingPeriods.AsNoTracking().OrderByDescending(period => period.StartDate)
            .Select(period => new AccountingPeriodSummary(period.Id, period.Name, period.StartDate,
                period.EndDate, period.IsClosed, period.ClosedAt)).ToListAsync(ct);
}

public sealed record CreateAccountingPeriodCommand(string Name, DateOnly StartDate, DateOnly EndDate)
    : IRequest<AccountingPeriodSummary>;

public sealed class CreateAccountingPeriodValidator : AbstractValidator<CreateAccountingPeriodCommand>
{
    public CreateAccountingPeriodValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.StartDate).NotEmpty();
        RuleFor(command => command.EndDate).NotEmpty().GreaterThanOrEqualTo(command => command.StartDate);
    }
}

public sealed class CreateAccountingPeriodHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateAccountingPeriodCommand, AccountingPeriodSummary>
{
    public async Task<AccountingPeriodSummary> Handle(CreateAccountingPeriodCommand request, CancellationToken ct)
    {
        var start = request.StartDate;
        var end = request.EndDate;
        var normalizedName = request.Name.Trim().ToLower();
        if (await db.AccountingPeriods.AnyAsync(period => period.Name.ToLower() == normalizedName, ct))
            throw new ConflictException($"Accounting period '{request.Name.Trim()}' already exists.");
        if (await db.AccountingPeriods.AnyAsync(period => period.StartDate <= end && period.EndDate >= start, ct))
            throw new ConflictException("Accounting periods cannot overlap.");
        var period = AccountingPeriod.Create(request.Name, start, end);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return new(period.Id, period.Name, period.StartDate, period.EndDate, period.IsClosed, period.ClosedAt);
    }
}

public sealed record CloseAccountingPeriodCommand(Guid Id, Guid? ApprovalId = null) : IRequest<AccountingPeriodSummary>;

public sealed class CloseAccountingPeriodHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CloseAccountingPeriodCommand, AccountingPeriodSummary>
{
    public async Task<AccountingPeriodSummary> Handle(CloseAccountingPeriodCommand request, CancellationToken ct)
    {
        await ApprovalGate.ValidateAsync(db, request.ApprovalId, ApprovalAction.ClosePeriod, request.Id.ToString(), new { id = request.Id }, ct);
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(item => item.Id == request.Id, ct)
            ?? throw new BuildingBlocks.Application.Exceptions.NotFoundException("Accounting period was not found.");
        if (period.IsClosed) throw new ConflictException("This accounting period is already closed.");
        if (await db.AccountingPeriods.AnyAsync(item => item.IsClosed && item.Id != period.Id
                && item.StartDate <= period.EndDate && item.EndDate >= period.StartDate, ct))
            throw new ConflictException("This period overlaps another closed period.");

        // Close nominal accounts into retained earnings before locking the period.
        // The journal uses the period end date and is committed with the lock in one SaveChanges.
        {
            var balances = await (from line in db.JournalLines.AsNoTracking()
                join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
                where entry.JournalDate >= period.StartDate && entry.JournalDate <= period.EndDate
                    && entry.SourceType != "YearEndClose"
                    && (account.Type == LedgerAccountType.Revenue || account.Type == LedgerAccountType.Expense)
                group line by new { line.AccountId, account.Type } into rows
                select new { rows.Key.AccountId, rows.Key.Type, Debit = rows.Sum(line => line.Debit), Credit = rows.Sum(line => line.Credit) })
                .ToListAsync(ct);
            var closeLines = balances.Select(balance =>
            {
                var net = balance.Credit - balance.Debit;
                return (balance.AccountId, Debit: net > 0 ? net : 0m, Credit: net < 0 ? -net : 0m,
                    Memo: (string?)"Year-end close");
            }).Where(line => line.Debit > 0 || line.Credit > 0).ToList();
            var profit = closeLines.Sum(line => line.Debit - line.Credit);
            if (closeLines.Count > 0)
            {
                var retainedEarningsId = await db.ChartAccounts.Where(account => account.Code == "3100" && account.IsActive)
                    .Select(account => (Guid?)account.Id).SingleOrDefaultAsync(ct)
                    ?? throw new ConflictException("Initialize the chart of accounts first; retained earnings account 3100 is required for year-end closing.");
                if (profit != 0)
                    closeLines.Add((retainedEarningsId, Debit: profit < 0 ? -profit : 0m,
                        Credit: profit > 0 ? profit : 0m, Memo: (string?)"Current year earnings"));
            }
            if (closeLines.Count > 1)
            {
                var closingJournal = JournalEntry.Post(period.EndDate, $"Year-end close · {period.Name}",
                    "YearEndClose", period.Id.ToString(), closeLines);
                db.JournalEntries.Add(closingJournal);
                db.JournalLines.AddRange(closingJournal.Lines);
            }
        }
        period.Close(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return new(period.Id, period.Name, period.StartDate, period.EndDate, period.IsClosed, period.ClosedAt);
    }
}
