using MediatR;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypesForDDL;

public sealed record GetProductTypesForDDL() : IRequest<IEnumerable<GetProductTypesForDDLSummary>>;

public sealed record GetProductTypesForDDLSummary(Guid Id, string Name);

