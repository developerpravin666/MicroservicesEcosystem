using MediatR;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModelsForDDL;

public sealed record GetProductModelsForDDL() : IRequest<IEnumerable<GetProductModelsForDDLSummary>>;

public sealed record GetProductModelsForDDLSummary(Guid Id, string Name, Guid BrandId, Guid ProductTypeId);


