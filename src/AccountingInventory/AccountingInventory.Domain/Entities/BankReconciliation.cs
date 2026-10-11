using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class BankReconciliation : AggregateRoot
{
    private readonly List<BankStatementLine> _lines = [];
    public Guid AccountId { get; private set; }
    public string StatementReference { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public bool IsFinalized { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public IReadOnlyCollection<BankStatementLine> Lines => _lines;

    public static BankReconciliation Create(Guid accountId, string reference, DateOnly startDate,
        DateOnly endDate, decimal openingBalance, decimal closingBalance,
        IReadOnlyCollection<(DateOnly Date, string Description, string? Reference, decimal Amount)> lines)
    {
        if (accountId == Guid.Empty || string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Bank account and statement reference are required.");
        if (startDate == default || endDate < startDate) throw new ArgumentException("Statement dates are invalid.");
        var entry = new BankReconciliation
        {
            Id = Guid.NewGuid(), IsActive = true, AccountId = accountId,
            StatementReference = reference.Trim(), StartDate = startDate, EndDate = endDate,
            OpeningBalance = decimal.Round(openingBalance, 2), ClosingBalance = decimal.Round(closingBalance, 2)
        };
        foreach (var line in lines)
        {
            if (line.Date < startDate || line.Date > endDate) throw new ArgumentException("Statement transaction date is outside the statement period.");
            entry._lines.Add(BankStatementLine.Create(entry.Id, line.Date, line.Description, line.Reference, line.Amount));
        }
        if (lines.Count == 0) throw new ArgumentException("A statement must contain at least one transaction.");
        if (decimal.Round(openingBalance + lines.Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero)
            != decimal.Round(closingBalance, 2, MidpointRounding.AwayFromZero))
            throw new ArgumentException("Opening balance plus statement transactions must equal closing balance.");
        return entry;
    }

    public void Finalize(DateTimeOffset finalizedAt, bool allLinesMatched)
    {
        if (IsFinalized) throw new InvalidOperationException("Bank reconciliation is already finalized.");
        if (!allLinesMatched) throw new InvalidOperationException("Match every statement transaction before finalizing.");
        IsFinalized = true;
        FinalizedAt = finalizedAt;
    }

    private BankReconciliation() { }
}

public sealed class BankStatementLine : AggregateRoot
{
    public Guid BankReconciliationId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public string Description { get; private set; } = null!;
    public string? Reference { get; private set; }
    /// <summary>Positive is money in; negative is money out.</summary>
    public decimal Amount { get; private set; }
    public Guid? JournalLineId { get; private set; }

    internal static BankStatementLine Create(Guid reconciliationId, DateOnly date, string description,
        string? reference, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description) || amount == 0) throw new ArgumentException("Statement line description and non-zero amount are required.");
        return new BankStatementLine
        {
            Id = Guid.NewGuid(), IsActive = true, BankReconciliationId = reconciliationId,
            TransactionDate = date, Description = description.Trim(), Reference = Normalize(reference),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
        };
    }

    public void Match(Guid journalLineId)
    {
        if (JournalLineId.HasValue) throw new InvalidOperationException("Statement line is already matched.");
        JournalLineId = journalLineId;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private BankStatementLine() { }
}
