using MediatR;

namespace AccountingInventory.Application.Vendors.Queries.GetVendorsForDDL;

public sealed record GetVendorsForDDL() : IRequest<IEnumerable<GetVendorsForDDLSummary>>;

public sealed record GetVendorsForDDLSummary(Guid Id, string Name);

