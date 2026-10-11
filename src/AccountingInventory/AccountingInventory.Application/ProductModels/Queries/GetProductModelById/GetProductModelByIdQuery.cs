using MediatR;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModelById;

/// <summary>Used by a signup/login UI to check slug availability or display ProductModel
/// info before authentication.</summary>
public sealed record GetProductModelByIdQuery(Guid Id) : IRequest<ProductModelSummary>;

public sealed record ProductModelSummary(Guid Id, Guid BrandId, Guid ProductTypeId, string Code, string Name, string Description, bool IsActive);


