namespace AccountingInventory.Application.FinanceVendors;

public sealed record FinanceVendorSummary(Guid Id, string Name, string Code, string Mobile, string Email,
    string ContactName, string ContactMobile, string? Description, bool IsActive);
