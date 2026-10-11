using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Commands.AddFinanceVendor;

public sealed record AddFinanceVendorCommand(string Name, string Code, string Mobile, string Email,
    string ContactName, string ContactMobile, string? Description) : IRequest<FinanceVendorSummary>;
