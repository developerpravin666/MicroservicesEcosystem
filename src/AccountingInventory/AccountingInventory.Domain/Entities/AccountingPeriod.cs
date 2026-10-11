using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class AccountingPeriod : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static AccountingPeriod Create(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Accounting period name is required.");
        if (startDate == default || endDate < startDate) throw new ArgumentException("Accounting period dates are invalid.");
        return new AccountingPeriod
        {
            Id = Guid.NewGuid(), IsActive = true, Name = name.Trim(),
            StartDate = startDate, EndDate = endDate
        };
    }

    public void Close(DateTimeOffset closedAt)
    {
        if (IsClosed) throw new InvalidOperationException("Accounting period is already closed.");
        IsClosed = true;
        ClosedAt = closedAt;
    }

    private AccountingPeriod() { }
}
