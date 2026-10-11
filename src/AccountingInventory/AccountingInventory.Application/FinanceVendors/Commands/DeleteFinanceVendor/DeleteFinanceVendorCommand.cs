using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Commands.DeleteFinanceVendor;

public sealed record DeleteFinanceVendorCommand(Guid Id) : IRequest;
