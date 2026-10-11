using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Commands.UpdateFinanceVendor;

public sealed record UpdateFinanceVendorCommand(Guid Id, string Name, string Code, string Mobile, string Email,
    string ContactName, string ContactMobile, string? Description, bool IsActive) : IRequest<FinanceVendorSummary>;
