using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record BankStatementLineInput(DateOnly TransactionDate, string Description, string? Reference, decimal Amount);
public sealed record CreateBankReconciliationCommand(Guid AccountId, string StatementReference,
    DateOnly StartDate, DateOnly EndDate, decimal OpeningBalance, decimal ClosingBalance,
    IReadOnlyList<BankStatementLineInput> Lines) : IRequest<BankReconciliationSummary>;
public sealed record BankReconciliationLineSummary(Guid Id, DateOnly TransactionDate, string Description,
    string? Reference, decimal Amount, Guid? JournalLineId);
public sealed record BankReconciliationSummary(Guid Id, Guid AccountId, string AccountCode, string AccountName,
    string StatementReference, DateOnly StartDate, DateOnly EndDate, decimal OpeningBalance,
    decimal ClosingBalance, bool IsFinalized, IReadOnlyList<BankReconciliationLineSummary> Lines);
public sealed record UnmatchedBankJournalLine(Guid Id, DateOnly JournalDate, string JournalNumber,
    string Description, decimal Amount, string? Memo);
public sealed record MatchBankStatementLineCommand(Guid ReconciliationId, Guid StatementLineId, Guid JournalLineId)
    : IRequest<BankReconciliationSummary>;
public sealed record FinalizeBankReconciliationCommand(Guid Id) : IRequest<BankReconciliationSummary>;
public sealed record GetBankReconciliationsQuery() : IRequest<IReadOnlyList<BankReconciliationSummary>>;
public sealed record GetUnmatchedBankJournalLinesQuery(Guid ReconciliationId) : IRequest<IReadOnlyList<UnmatchedBankJournalLine>>;

public sealed class CreateBankReconciliationValidator : AbstractValidator<CreateBankReconciliationCommand>
{
    public CreateBankReconciliationValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.StatementReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 5000);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
            line.RuleFor(x => x.Reference).MaximumLength(100);
            line.RuleFor(x => x.Amount).NotEqual(0);
        });
    }
}

public sealed class BankReconciliationManagementHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateBankReconciliationCommand, BankReconciliationSummary>,
      IRequestHandler<MatchBankStatementLineCommand, BankReconciliationSummary>,
      IRequestHandler<FinalizeBankReconciliationCommand, BankReconciliationSummary>,
      IRequestHandler<GetBankReconciliationsQuery, IReadOnlyList<BankReconciliationSummary>>,
      IRequestHandler<GetUnmatchedBankJournalLinesQuery, IReadOnlyList<UnmatchedBankJournalLine>>
{
    public async Task<BankReconciliationSummary> Handle(CreateBankReconciliationCommand request, CancellationToken ct)
    {
        var account = await db.ChartAccounts.SingleOrDefaultAsync(item => item.Id == request.AccountId && item.IsActive, ct)
            ?? throw new NotFoundException("Active bank ledger account was not found.");
        if (account.Type != LedgerAccountType.Asset || account.NormalBalance != LedgerBalanceSide.Debit)
            throw new ConflictException("Reconciliation requires an active asset account with a debit normal balance.");
        if (await db.BankReconciliations.AnyAsync(item => item.AccountId == account.Id
                && item.StartDate <= request.EndDate && item.EndDate >= request.StartDate, ct))
            throw new ConflictException("A reconciliation already exists for an overlapping date range.");
        var reconciliation = BankReconciliation.Create(account.Id, request.StatementReference,
            request.StartDate, request.EndDate, request.OpeningBalance, request.ClosingBalance,
            request.Lines.Select(line => (line.TransactionDate, line.Description, line.Reference, line.Amount)).ToArray());
        db.BankReconciliations.Add(reconciliation);
        db.BankStatementLines.AddRange(reconciliation.Lines);
        await db.SaveChangesAsync(ct);
        return await MapAsync(reconciliation.Id, ct);
    }

    public async Task<BankReconciliationSummary> Handle(MatchBankStatementLineCommand request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations.SingleOrDefaultAsync(item => item.Id == request.ReconciliationId, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");
        if (reconciliation.IsFinalized) throw new ConflictException("Finalized reconciliations cannot be changed.");
        var statementLine = await db.BankStatementLines.SingleOrDefaultAsync(item => item.Id == request.StatementLineId
            && item.BankReconciliationId == reconciliation.Id, ct) ?? throw new NotFoundException("Statement line was not found.");
        var journalLine = await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where line.Id == request.JournalLineId && line.AccountId == reconciliation.AccountId
                && entry.JournalDate >= reconciliation.StartDate && entry.JournalDate <= reconciliation.EndDate
            select new { line.Id, line.Debit, line.Credit }).SingleOrDefaultAsync(ct)
            ?? throw new ConflictException("Choose a journal line from this bank account and statement date range.");
        if (await db.BankStatementLines.AnyAsync(item => item.JournalLineId == journalLine.Id, ct))
            throw new ConflictException("This ledger transaction is already matched.");
        var ledgerAmount = decimal.Round(journalLine.Debit - journalLine.Credit, 2);
        if (ledgerAmount != statementLine.Amount)
            throw new ConflictException("Statement and ledger amounts must match exactly; record any bank fee as a separate journal first.");
        statementLine.Match(journalLine.Id);
        await db.SaveChangesAsync(ct);
        return await MapAsync(reconciliation.Id, ct);
    }

    public async Task<BankReconciliationSummary> Handle(FinalizeBankReconciliationCommand request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations.SingleOrDefaultAsync(item => item.Id == request.Id, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");
        var allMatched = !await db.BankStatementLines.AnyAsync(line => line.BankReconciliationId == reconciliation.Id
            && line.JournalLineId == null, ct);
        if (allMatched)
        {
            var openingBook = await (from line in db.JournalLines.AsNoTracking()
                join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                where line.AccountId == reconciliation.AccountId && entry.JournalDate < reconciliation.StartDate
                select line.Debit - line.Credit).SumAsync(ct);
            var priorOutstanding = await (from line in db.JournalLines.AsNoTracking()
                join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                where line.AccountId == reconciliation.AccountId && entry.JournalDate < reconciliation.StartDate
                    && !db.BankStatementLines.Any(match => match.JournalLineId == line.Id)
                select line.Debit - line.Credit).SumAsync(ct);
            var expectedOpening = decimal.Round(openingBook - priorOutstanding, 2);
            if (expectedOpening != reconciliation.OpeningBalance)
                throw new ConflictException($"Statement opening balance {reconciliation.OpeningBalance:0.00} does not match expected balance {expectedOpening:0.00}. Reconcile or record the opening balance first.");

            var closingBook = await (from line in db.JournalLines.AsNoTracking()
                join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                where line.AccountId == reconciliation.AccountId && entry.JournalDate <= reconciliation.EndDate
                select line.Debit - line.Credit).SumAsync(ct);
            var periodOutstanding = await (from line in db.JournalLines.AsNoTracking()
                join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
                where line.AccountId == reconciliation.AccountId && entry.JournalDate >= reconciliation.StartDate
                    && entry.JournalDate <= reconciliation.EndDate
                    && !db.BankStatementLines.Any(match => match.JournalLineId == line.Id)
                select line.Debit - line.Credit).SumAsync(ct);
            var expectedClosing = decimal.Round(closingBook - periodOutstanding, 2);
            if (expectedClosing != reconciliation.ClosingBalance)
                throw new ConflictException($"Statement closing balance {reconciliation.ClosingBalance:0.00} does not reconcile to the ledger. Expected {expectedClosing:0.00} after outstanding ledger items.");
        }
        try { reconciliation.Finalize(DateTimeOffset.UtcNow, allMatched); }
        catch (InvalidOperationException exception) { throw new ConflictException(exception.Message); }
        await db.SaveChangesAsync(ct);
        return await MapAsync(reconciliation.Id, ct);
    }

    public async Task<IReadOnlyList<BankReconciliationSummary>> Handle(GetBankReconciliationsQuery request, CancellationToken ct)
    {
        var ids = await db.BankReconciliations.AsNoTracking().OrderByDescending(item => item.EndDate)
            .Select(item => item.Id).ToListAsync(ct);
        var rows = new List<BankReconciliationSummary>(ids.Count);
        foreach (var id in ids) rows.Add(await MapAsync(id, ct));
        return rows;
    }

    public async Task<IReadOnlyList<UnmatchedBankJournalLine>> Handle(GetUnmatchedBankJournalLinesQuery request, CancellationToken ct)
    {
        var reconciliation = await db.BankReconciliations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ReconciliationId, ct)
            ?? throw new NotFoundException("Bank reconciliation was not found.");
        return await (from line in db.JournalLines.AsNoTracking()
            join entry in db.JournalEntries.AsNoTracking() on line.JournalEntryId equals entry.Id
            where line.AccountId == reconciliation.AccountId && entry.JournalDate >= reconciliation.StartDate
                && entry.JournalDate <= reconciliation.EndDate
                && !db.BankStatementLines.Any(match => match.JournalLineId == line.Id)
            orderby entry.JournalDate
            select new UnmatchedBankJournalLine(line.Id, entry.JournalDate, entry.JournalNumber,
                entry.Description, line.Debit - line.Credit, line.Memo)).ToListAsync(ct);
    }

    private async Task<BankReconciliationSummary> MapAsync(Guid id, CancellationToken ct)
    {
        var item = await db.BankReconciliations.AsNoTracking().SingleAsync(row => row.Id == id, ct);
        var account = await db.ChartAccounts.AsNoTracking().SingleAsync(row => row.Id == item.AccountId, ct);
        var lines = await db.BankStatementLines.AsNoTracking().Where(line => line.BankReconciliationId == item.Id)
            .OrderBy(line => line.TransactionDate)
            .Select(line => new BankReconciliationLineSummary(line.Id, line.TransactionDate, line.Description,
                line.Reference, line.Amount, line.JournalLineId)).ToListAsync(ct);
        return new(item.Id, item.AccountId, account.Code, account.Name, item.StatementReference,
            item.StartDate, item.EndDate, item.OpeningBalance, item.ClosingBalance, item.IsFinalized, lines);
    }
}
