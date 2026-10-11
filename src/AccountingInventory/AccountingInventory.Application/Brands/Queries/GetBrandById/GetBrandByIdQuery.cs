using MediatR;

namespace AccountingInventory.Application.Brands.Queries.GetBrandById;

/// <summary>Used by a signup/login UI to check slug availability or display brand
/// info before authentication.</summary>
public sealed record GetBrandByIdQuery(Guid Id) : IRequest<BrandSummary>;

public sealed record BrandSummary(Guid Id, string Name, string Description, bool IsActive);
