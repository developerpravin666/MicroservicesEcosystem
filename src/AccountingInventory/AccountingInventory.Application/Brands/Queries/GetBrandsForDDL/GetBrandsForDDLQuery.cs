using MediatR;

namespace AccountingInventory.Application.Brands.Queries.GetBrandsForDDL;

public sealed record GetBrandsForDDL() : IRequest<IEnumerable<GetBrandsForDDLSummary>>;

public sealed record GetBrandsForDDLSummary(Guid Id, string Name);

