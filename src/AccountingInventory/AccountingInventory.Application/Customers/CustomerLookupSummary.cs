namespace AccountingInventory.Application.Customers;

public sealed record CustomerLookupSummary(
    Guid Id,
    string Name,
    string Mobile,
    string Address,
    string? Email, string? Gstin = null, string? StateCode = null, string? StateName = null);
