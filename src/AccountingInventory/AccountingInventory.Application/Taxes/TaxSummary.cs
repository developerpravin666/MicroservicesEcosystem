namespace AccountingInventory.Application.Taxes;

public sealed record TaxSummary(Guid Id, decimal Cgst, decimal Sgst, decimal TotalTax, bool IsActive,
    DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset? ModifiedAt, Guid? ModifiedBy);
