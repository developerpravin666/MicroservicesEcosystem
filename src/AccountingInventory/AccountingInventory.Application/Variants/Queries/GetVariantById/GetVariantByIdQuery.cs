using MediatR;

namespace AccountingInventory.Application.Variants.Queries.GetVariantById;

/// <summary>Used by a signup/login UI to check slug availability or display variant
/// info before authentication.</summary>
public sealed record GetVariantByIdQuery(Guid Id) : IRequest<VariantSummary>;

public sealed record VariantSummary(Guid Id, string Name, string Description, bool IsActive);
