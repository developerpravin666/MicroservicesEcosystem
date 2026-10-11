using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorById;

public sealed record GetFinanceVendorByIdQuery(Guid Id) : IRequest<FinanceVendorSummary>;
