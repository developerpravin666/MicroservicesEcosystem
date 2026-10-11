using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum LedgerAccountType { Asset, Liability, Equity, Revenue, Expense }
public enum LedgerBalanceSide { Debit, Credit }

public sealed class ChartAccount : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public LedgerAccountType Type { get; private set; }
    public LedgerBalanceSide NormalBalance { get; private set; }
    public bool IsSystem { get; private set; }

    public static ChartAccount Create(string code, string name, LedgerAccountType type,
        LedgerBalanceSide normalBalance, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20) throw new ArgumentException("Account code is required and must be at most 20 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150) throw new ArgumentException("Account name is required and must be at most 150 characters.");
        return new ChartAccount { Id = Guid.NewGuid(), IsActive = true, Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), Type = type, NormalBalance = normalBalance, IsSystem = isSystem };
    }

    public void Deactivate()
    {
        if (IsSystem) throw new InvalidOperationException("System accounts cannot be deactivated.");
        IsActive = false;
    }

    private ChartAccount() { }
}
