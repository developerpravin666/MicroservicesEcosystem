using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public static class LedgerAccountDefaults
{
    public static readonly (string Code, string Name, LedgerAccountType Type, LedgerBalanceSide Side)[] All =
    [
        ("1000", "Cash on Hand", LedgerAccountType.Asset, LedgerBalanceSide.Debit),
        ("1010", "Bank Account", LedgerAccountType.Asset, LedgerBalanceSide.Debit),
        ("1100", "Accounts Receivable", LedgerAccountType.Asset, LedgerBalanceSide.Debit),
        ("1200", "Inventory", LedgerAccountType.Asset, LedgerBalanceSide.Debit),
        ("2000", "Accounts Payable", LedgerAccountType.Liability, LedgerBalanceSide.Credit),
        ("2100", "GST Payable", LedgerAccountType.Liability, LedgerBalanceSide.Credit),
        ("2200", "GST Input Credit", LedgerAccountType.Asset, LedgerBalanceSide.Debit),
        ("3000", "Owner's Equity", LedgerAccountType.Equity, LedgerBalanceSide.Credit),
        ("3100", "Retained Earnings", LedgerAccountType.Equity, LedgerBalanceSide.Credit),
        ("4000", "Sales Revenue", LedgerAccountType.Revenue, LedgerBalanceSide.Credit),
        ("4100", "Sales Returns and Discounts", LedgerAccountType.Revenue, LedgerBalanceSide.Debit),
        ("5000", "Cost of Goods Sold", LedgerAccountType.Expense, LedgerBalanceSide.Debit),
        ("5100", "Operating Expenses", LedgerAccountType.Expense, LedgerBalanceSide.Debit),
        ("4900", "Gain on Asset Disposal", LedgerAccountType.Revenue, LedgerBalanceSide.Credit),
        ("5200", "Loss on Asset Disposal", LedgerAccountType.Expense, LedgerBalanceSide.Debit)
    ];
}

public sealed record LedgerAccountSummary(Guid Id, string Code, string Name, LedgerAccountType Type,
    LedgerBalanceSide NormalBalance, bool IsActive, bool IsSystem);

public sealed record GetLedgerAccountsQuery(bool ActiveOnly = true) : IRequest<IReadOnlyList<LedgerAccountSummary>>;

public sealed class GetLedgerAccountsHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetLedgerAccountsQuery, IReadOnlyList<LedgerAccountSummary>>
{
    public async Task<IReadOnlyList<LedgerAccountSummary>> Handle(GetLedgerAccountsQuery request, CancellationToken ct)
        => await db.ChartAccounts.AsNoTracking()
            .Where(account => !request.ActiveOnly || account.IsActive)
            .OrderBy(account => account.Code)
            .Select(account => new LedgerAccountSummary(account.Id, account.Code, account.Name,
                account.Type, account.NormalBalance, account.IsActive, account.IsSystem))
            .ToListAsync(ct);
}

public sealed record InitializeChartOfAccountsCommand() : IRequest<int>;

public sealed class InitializeChartOfAccountsHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<InitializeChartOfAccountsCommand, int>
{
    public async Task<int> Handle(InitializeChartOfAccountsCommand request, CancellationToken ct)
    {
        var existing = await db.ChartAccounts.Select(account => account.Code).ToListAsync(ct);
        var accounts = LedgerAccountDefaults.All.Where(item => !existing.Contains(item.Code))
            .Select(item => ChartAccount.Create(item.Code, item.Name, item.Type, item.Side, isSystem: true)).ToArray();
        if (accounts.Length == 0) return 0;
        db.ChartAccounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);
        return accounts.Length;
    }
}

public sealed record CreateLedgerAccountCommand(string Code, string Name, LedgerAccountType Type,
    LedgerBalanceSide NormalBalance) : IRequest<LedgerAccountSummary>;

public sealed class CreateLedgerAccountValidator : AbstractValidator<CreateLedgerAccountCommand>
{
    public CreateLedgerAccountValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.NormalBalance).IsInEnum();
    }
}

public sealed class CreateLedgerAccountHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateLedgerAccountCommand, LedgerAccountSummary>
{
    public async Task<LedgerAccountSummary> Handle(CreateLedgerAccountCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.ChartAccounts.AnyAsync(account => account.Code == code, ct))
            throw new ConflictException($"Account code '{code}' already exists.");
        var account = ChartAccount.Create(code, request.Name, request.Type, request.NormalBalance);
        db.ChartAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return new(account.Id, account.Code, account.Name, account.Type, account.NormalBalance, account.IsActive, account.IsSystem);
    }
}

public sealed record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Memo = null, Guid? DimensionId = null);
public sealed record PostJournalCommand(DateOnly JournalDate, string Description, string? SourceType,
    string? SourceId, IReadOnlyCollection<JournalLineInput> Lines, Guid? ApprovalId = null) : IRequest<JournalSummary>;
public sealed record JournalLineSummary(Guid AccountId, string AccountCode, string AccountName,
    decimal Debit, decimal Credit, string? Memo, Guid? DimensionId = null);
public sealed record JournalSummary(Guid Id, string JournalNumber, DateOnly JournalDate, string Description,
    string? SourceType, string? SourceId, DateTimeOffset PostedAt, Guid? ReversalOfJournalEntryId, bool IsReversed,
    IReadOnlyList<JournalLineSummary> Lines, decimal TotalDebit, decimal TotalCredit);

public sealed class PostJournalValidator : AbstractValidator<PostJournalCommand>
{
    public PostJournalValidator()
    {
        RuleFor(command => command.JournalDate).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(500);
        RuleFor(command => command.SourceType).MaximumLength(60);
        RuleFor(command => command.SourceId).MaximumLength(128);
        RuleFor(command => command.Lines).NotNull().Must(lines => lines.Count >= 2)
            .WithMessage("A journal requires at least two lines.");
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.AccountId).NotEmpty();
            line.RuleFor(item => item.Debit).GreaterThanOrEqualTo(0);
            line.RuleFor(item => item.Credit).GreaterThanOrEqualTo(0);
            line.RuleFor(item => item.Memo).MaximumLength(250);
        });
        RuleFor(command => command).Must(command => command.Lines != null
            && command.Lines.All(line => (decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero) > 0)
                != (decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero) > 0)))
            .WithMessage("Each journal line must have one positive debit or credit amount.");
        RuleFor(command => command).Must(command => command.Lines != null
            && command.Lines.Sum(line => decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero))
                == command.Lines.Sum(line => decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero))
            && command.Lines.Sum(line => decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero)) > 0)
            .WithMessage("Journal debits and credits must be equal and greater than zero.");
    }
}

public sealed class PostJournalHandler(IAccountingInventoryDbContext db) : IRequestHandler<PostJournalCommand, JournalSummary>
{
    public async Task<JournalSummary> Handle(PostJournalCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.JournalDate, ct);
        if (!string.IsNullOrWhiteSpace(request.SourceType)
            && !string.Equals(request.SourceType, "Manual", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.SourceType, "FinancialCorrection", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Manual journals may only use Manual or FinancialCorrection as their source type.");
        var isCorrection = string.Equals(request.SourceType, "FinancialCorrection", StringComparison.OrdinalIgnoreCase);
        if (isCorrection && request.ApprovalId is null)
            throw new ConflictException("Financial corrections require an approved request.");
        await ApprovalGate.ValidateAsync(db, request.ApprovalId,
            isCorrection ? ApprovalAction.FinancialCorrection : ApprovalAction.Journal,
            request.SourceId ?? request.Description,
            new { journalDate = request.JournalDate, description = request.Description, sourceType = request.SourceType,
                sourceId = request.SourceId, lines = request.Lines }, ct);
        var accountIds = request.Lines.Select(line => line.AccountId).Distinct().ToArray();
        var accounts = await db.ChartAccounts.AsNoTracking()
            .Where(account => accountIds.Contains(account.Id) && account.IsActive)
            .ToDictionaryAsync(account => account.Id, ct);
        if (accounts.Count != accountIds.Length)
            throw new ConflictException("Every journal line must reference an active ledger account.");
        if (!isCorrection && accounts.Values.Any(a => a.Code is "1100" or "1200" or "2000"))
            throw new ConflictException("Receivables, payables and inventory require their source workflow or an approved FinancialCorrection journal.");
        var dimensionIds = request.Lines.Where(line => line.DimensionId.HasValue).Select(line => line.DimensionId!.Value).Distinct().ToArray();
        var dimensions = await db.AccountingDimensions.Where(x => dimensionIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        if (dimensions.Count != dimensionIds.Length) throw new ConflictException("Every journal dimension must be active.");

        var entry = JournalEntry.Post(request.JournalDate, request.Description, request.SourceType,
            request.SourceId, request.Lines.Select(line => (line.AccountId, line.Debit, line.Credit, line.Memo)).ToArray());
        var lineIndex = 0;
        foreach (var line in entry.Lines) line.AssignDimension(request.Lines.ElementAt(lineIndex++).DimensionId);
        db.JournalEntries.Add(entry);
        db.JournalLines.AddRange(entry.Lines);
        await db.SaveChangesAsync(ct);
        var rows = entry.Lines.Select(line => new JournalLineSummary(line.AccountId,
            accounts[line.AccountId].Code, accounts[line.AccountId].Name, line.Debit, line.Credit, line.Memo, line.DimensionId)).ToArray();
        return new(entry.Id, entry.JournalNumber, entry.JournalDate, entry.Description,
            entry.SourceType, entry.SourceId, entry.PostedAt, entry.ReversalOfJournalEntryId, false, rows,
            rows.Sum(line => line.Debit), rows.Sum(line => line.Credit));
    }
}

public sealed record GetJournalsQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<JournalSummary>>;

public sealed class GetJournalsHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetJournalsQuery, PagedResult<JournalSummary>>
{
    public async Task<PagedResult<JournalSummary>> Handle(GetJournalsQuery request, CancellationToken ct)
    {
        var query = db.JournalEntries.AsNoTracking().OrderByDescending(entry => entry.JournalDate)
            .ThenByDescending(entry => entry.PostedAt);
        var count = await query.CountAsync(ct);
        var page = Math.Max(request.Page, 1);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var entries = await query.Skip((page - 1) * size).Take(size)
            .Select(entry => new { entry.Id, entry.JournalNumber, entry.JournalDate, entry.Description,
                entry.SourceType, entry.SourceId, entry.PostedAt, entry.ReversalOfJournalEntryId })
            .ToListAsync(ct);
        var ids = entries.Select(entry => entry.Id).ToArray();
        var reversedIds = await db.JournalEntries.AsNoTracking()
            .Where(entry => entry.ReversalOfJournalEntryId.HasValue && ids.Contains(entry.ReversalOfJournalEntryId.Value))
            .Select(entry => entry.ReversalOfJournalEntryId!.Value).ToListAsync(ct);
        var reversed = reversedIds.ToHashSet();
        var lines = await (from line in db.JournalLines.AsNoTracking()
            join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
            where ids.Contains(line.JournalEntryId)
            select new { line.JournalEntryId, line.AccountId, account.Code, account.Name, line.Debit, line.Credit, line.Memo, line.DimensionId })
            .ToListAsync(ct);
        var result = entries.Select(entry =>
        {
            var rows = lines.Where(line => line.JournalEntryId == entry.Id)
                .Select(line => new JournalLineSummary(line.AccountId, line.Code, line.Name, line.Debit, line.Credit, line.Memo, line.DimensionId)).ToArray();
            return new JournalSummary(entry.Id, entry.JournalNumber, entry.JournalDate, entry.Description,
                entry.SourceType, entry.SourceId, entry.PostedAt, entry.ReversalOfJournalEntryId, reversed.Contains(entry.Id), rows,
                rows.Sum(line => line.Debit), rows.Sum(line => line.Credit));
        }).ToArray();
        return new PagedResult<JournalSummary>(result, page, size, count,
            count == 0 ? 0 : (int)Math.Ceiling(count / (double)size));
    }
}

public sealed record ReverseJournalCommand(Guid JournalEntryId, DateOnly ReversalDate, string? Reason = null)
    : IRequest<JournalSummary>;

public sealed class ReverseJournalValidator : AbstractValidator<ReverseJournalCommand>
{
    public ReverseJournalValidator()
    {
        RuleFor(command => command.JournalEntryId).NotEmpty();
        RuleFor(command => command.ReversalDate).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(300);
    }
}

public sealed class ReverseJournalHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<ReverseJournalCommand, JournalSummary>
{
    public async Task<JournalSummary> Handle(ReverseJournalCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.ReversalDate, ct);
        var original = await db.JournalEntries.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == request.JournalEntryId, ct)
            ?? throw new BuildingBlocks.Application.Exceptions.NotFoundException("Journal entry was not found.");
        if (!string.IsNullOrWhiteSpace(original.SourceType)
            && !string.Equals(original.SourceType, "Manual", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(original.SourceType, "FinancialCorrection", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("System journals must be corrected through their source workflow so stock and balances remain consistent.");
        if (request.ReversalDate < original.JournalDate)
            throw new ConflictException("A reversal cannot precede the original journal date.");
        if (await db.JournalEntries.AnyAsync(entry => entry.ReversalOfJournalEntryId == original.Id, ct))
            throw new ConflictException("This journal entry has already been reversed.");
        var lines = await db.JournalLines.AsNoTracking().Where(line => line.JournalEntryId == original.Id)
            .Select(line => new { line.AccountId, line.Debit, line.Credit, line.Memo, line.DimensionId }).ToListAsync(ct);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var description = $"Reversal of {original.JournalNumber}" + (reason is null ? string.Empty : $": {reason}");
        var reversal = JournalEntry.Post(request.ReversalDate, description, "JournalReversal", original.Id.ToString(),
            lines.Select(line => (line.AccountId, Debit: line.Credit, Credit: line.Debit,
                Memo: line.Memo)).ToArray(), original.Id);
        var reversalIndex = 0;
        foreach (var line in reversal.Lines) line.AssignDimension(lines[reversalIndex++].DimensionId);
        db.JournalEntries.Add(reversal);
        db.JournalLines.AddRange(reversal.Lines);
        await db.SaveChangesAsync(ct);

        var accounts = await db.ChartAccounts.AsNoTracking().Where(account => lines.Select(line => line.AccountId).Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, ct);
        var summaries = reversal.Lines.Select(line => new JournalLineSummary(line.AccountId,
            accounts[line.AccountId].Code, accounts[line.AccountId].Name, line.Debit, line.Credit, line.Memo, line.DimensionId)).ToArray();
        return new(reversal.Id, reversal.JournalNumber, reversal.JournalDate, reversal.Description,
            reversal.SourceType, reversal.SourceId, reversal.PostedAt, original.Id, false, summaries,
            summaries.Sum(line => line.Debit), summaries.Sum(line => line.Credit));
    }
}

public sealed record TrialBalanceRow(string Code, string Name, LedgerAccountType Type,
    decimal DebitBalance, decimal CreditBalance);
public sealed record TrialBalanceSummary(DateOnly AsOfDate, IReadOnlyList<TrialBalanceRow> Accounts,
    decimal TotalDebits, decimal TotalCredits);
public sealed record GetTrialBalanceQuery(DateOnly? AsOfDate = null) : IRequest<TrialBalanceSummary>;

public sealed class GetTrialBalanceHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetTrialBalanceQuery, TrialBalanceSummary>
{
    public async Task<TrialBalanceSummary> Handle(GetTrialBalanceQuery request, CancellationToken ct)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var balances = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where entry.JournalDate <= asOf
            group line by line.AccountId into accountLines
            select new { AccountId = accountLines.Key, Debit = accountLines.Sum(line => line.Debit), Credit = accountLines.Sum(line => line.Credit) })
            .ToDictionaryAsync(row => row.AccountId, ct);
        var accounts = await db.ChartAccounts.AsNoTracking().OrderBy(account => account.Code)
            .Select(account => new { account.Id, account.Code, account.Name, account.Type })
            .ToListAsync(ct);
        var rows = accounts.Select(account =>
        {
            var net = balances.TryGetValue(account.Id, out var totals) ? totals.Debit - totals.Credit : 0m;
            return new TrialBalanceRow(account.Code, account.Name, account.Type,
                Math.Max(net, 0m), Math.Max(-net, 0m));
        }).ToArray();
        return new TrialBalanceSummary(asOf, rows, rows.Sum(row => row.DebitBalance), rows.Sum(row => row.CreditBalance));
    }
}

public sealed record FinancialStatementLine(string Code, string Name, decimal Amount);
public sealed record FinancialStatementsSummary(DateOnly FromDate, DateOnly ToDate,
    IReadOnlyList<FinancialStatementLine> Revenue, decimal TotalRevenue,
    IReadOnlyList<FinancialStatementLine> Expenses, decimal TotalExpenses, decimal NetIncome,
    IReadOnlyList<FinancialStatementLine> Assets, decimal TotalAssets,
    IReadOnlyList<FinancialStatementLine> Liabilities, decimal TotalLiabilities,
    IReadOnlyList<FinancialStatementLine> Equity, decimal TotalEquity,
    decimal CurrentEarnings, decimal BalanceDifference);
public sealed record GetFinancialStatementsQuery(DateOnly? FromDate = null, DateOnly? ToDate = null)
    : IRequest<FinancialStatementsSummary>;

public sealed class GetFinancialStatementsHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetFinancialStatementsQuery, FinancialStatementsSummary>
{
    public async Task<FinancialStatementsSummary> Handle(GetFinancialStatementsQuery request, CancellationToken ct)
    {
        var toDate = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? new DateOnly(toDate.Year, 1, 1);
        if (fromDate > toDate) throw new ConflictException("Statement start date must be on or before its end date.");

        var accounts = await db.ChartAccounts.AsNoTracking()
            .OrderBy(account => account.Code)
            .Select(account => new { account.Id, account.Code, account.Name, account.Type })
            .ToListAsync(ct);
        var totals = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            join account in db.ChartAccounts.AsNoTracking() on line.AccountId equals account.Id
            where entry.JournalDate <= toDate

            group line by line.AccountId into accountLines
            select new { AccountId = accountLines.Key, Debit = accountLines.Sum(line => line.Debit), Credit = accountLines.Sum(line => line.Credit) })
            .ToDictionaryAsync(row => row.AccountId, ct);
        var periodTotals = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where entry.JournalDate >= fromDate && entry.JournalDate <= toDate && entry.SourceType != "YearEndClose"
            group line by line.AccountId into accountLines
            select new { AccountId = accountLines.Key, Debit = accountLines.Sum(line => line.Debit), Credit = accountLines.Sum(line => line.Credit) })
            .ToDictionaryAsync(row => row.AccountId, ct);

        decimal Net(IReadOnlyDictionary<Guid, (decimal Debit, decimal Credit)> values, Guid accountId, bool creditNormal)
        {
            if (!values.TryGetValue(accountId, out var value)) return 0m;
            return creditNormal ? value.Credit - value.Debit : value.Debit - value.Credit;
        }
        var balances = totals.ToDictionary(pair => pair.Key, pair => (Debit: pair.Value.Debit, Credit: pair.Value.Credit));
        var period = periodTotals.ToDictionary(pair => pair.Key, pair => (Debit: pair.Value.Debit, Credit: pair.Value.Credit));
        FinancialStatementLine[] Select(LedgerAccountType type, IReadOnlyDictionary<Guid, (decimal Debit, decimal Credit)> values,
            bool creditNormal) => accounts.Where(account => account.Type == type)
            .Select(account => new FinancialStatementLine(account.Code, account.Name, Net(values, account.Id, creditNormal)))
            .Where(line => line.Amount != 0m).ToArray();

        var revenue = Select(LedgerAccountType.Revenue, period, creditNormal: true);
        var expenses = Select(LedgerAccountType.Expense, period, creditNormal: false);
        var assets = Select(LedgerAccountType.Asset, balances, creditNormal: false);
        var liabilities = Select(LedgerAccountType.Liability, balances, creditNormal: true);
        var equity = Select(LedgerAccountType.Equity, balances, creditNormal: true);
        var cumulativeRevenue = Select(LedgerAccountType.Revenue, balances, creditNormal: true).Sum(line => line.Amount);
        var cumulativeExpenses = Select(LedgerAccountType.Expense, balances, creditNormal: false).Sum(line => line.Amount);
        var totalRevenue = revenue.Sum(line => line.Amount);
        var totalExpenses = expenses.Sum(line => line.Amount);
        var netIncome = totalRevenue - totalExpenses;
        var currentEarnings = cumulativeRevenue - cumulativeExpenses;
        var totalAssets = assets.Sum(line => line.Amount);
        var totalLiabilities = liabilities.Sum(line => line.Amount);
        var totalEquity = equity.Sum(line => line.Amount);
        return new FinancialStatementsSummary(fromDate, toDate, revenue, totalRevenue, expenses, totalExpenses,
            netIncome, assets, totalAssets, liabilities, totalLiabilities, equity, totalEquity, currentEarnings,
            totalAssets - totalLiabilities - totalEquity - currentEarnings);
    }
}
