using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class JournalEntry : AggregateRoot
{
    public string JournalNumber { get; private set; } = null!;
    public DateOnly JournalDate { get; private set; }
    public string Description { get; private set; } = null!;
    public string? SourceType { get; private set; }
    public string? SourceId { get; private set; }
    public Guid? ReversalOfJournalEntryId { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public IReadOnlyCollection<JournalLine> Lines => _lines;
    private readonly List<JournalLine> _lines = [];

    public static JournalEntry Post(DateOnly date, string description, string? sourceType,
        string? sourceId, IReadOnlyCollection<(Guid AccountId, decimal Debit, decimal Credit, string? Memo)> lines,
        Guid? reversalOfJournalEntryId = null)
    {
        if (date == default) throw new ArgumentException("Journal date is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Journal description is required.");
        if (lines.Count < 2) throw new ArgumentException("A journal requires at least two lines.");
        var roundedLines = lines.Select(line => (line.AccountId,
            Debit: decimal.Round(line.Debit, 2, MidpointRounding.AwayFromZero),
            Credit: decimal.Round(line.Credit, 2, MidpointRounding.AwayFromZero), line.Memo)).ToArray();
        if (roundedLines.Any(line => line.AccountId == Guid.Empty || line.Debit < 0 || line.Credit < 0 || (line.Debit == 0) == (line.Credit == 0)))
            throw new ArgumentException("Each journal line must have one positive debit or credit amount.");
        var debits = roundedLines.Sum(line => line.Debit);
        var credits = roundedLines.Sum(line => line.Credit);
        if (debits <= 0 || debits != credits) throw new ArgumentException("Journal debits and credits must be equal and greater than zero.");

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), IsActive = true,
            JournalNumber = $"JE-{date:yyyyMMdd}-{Guid.NewGuid():N}".ToUpperInvariant(),
            JournalDate = date, Description = description.Trim(),
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim(),
            SourceId = string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim(),
            ReversalOfJournalEntryId = reversalOfJournalEntryId,
            PostedAt = DateTimeOffset.UtcNow
        };
        foreach (var line in roundedLines)
            entry._lines.Add(JournalLine.Create(entry.Id, line.AccountId, line.Debit, line.Credit, line.Memo));
        return entry;
    }

    private JournalEntry() { }
}

public sealed class JournalLine : AggregateRoot
{
    public Guid JournalEntryId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string? Memo { get; private set; }
    public Guid? DimensionId { get; private set; }

    public void AssignDimension(Guid? dimensionId) => DimensionId = dimensionId;

    internal static JournalLine Create(Guid journalEntryId, Guid accountId, decimal debit, decimal credit, string? memo)
        => new()
        {
            Id = Guid.NewGuid(), IsActive = true, JournalEntryId = journalEntryId,
            AccountId = accountId, Debit = decimal.Round(debit, 2, MidpointRounding.AwayFromZero),
            Credit = decimal.Round(credit, 2, MidpointRounding.AwayFromZero),
            Memo = string.IsNullOrWhiteSpace(memo) ? null : memo.Trim()
        };

    private JournalLine() { }
}
