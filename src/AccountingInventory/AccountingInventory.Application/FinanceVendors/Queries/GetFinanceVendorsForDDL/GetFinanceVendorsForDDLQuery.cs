using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorsForDDL;

public sealed record GetFinanceVendorsForDDLQuery : IRequest<IEnumerable<GetFinanceVendorsForDDLSummary>>;
public sealed record GetFinanceVendorsForDDLSummary(Guid Id, string Name, string Code);
