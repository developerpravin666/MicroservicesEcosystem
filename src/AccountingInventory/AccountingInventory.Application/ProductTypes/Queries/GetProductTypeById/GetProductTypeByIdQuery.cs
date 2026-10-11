using MediatR;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypeById;

/// <summary>Used by a signup/login UI to check slug availability or display ProductType
/// info before authentication.</summary>
public sealed record GetProductTypeByIdQuery(Guid Id) : IRequest<ProductTypeSummary>;

public sealed record ProductTypeSummary(Guid Id, string Name, string Description, bool IsActive);

