using MediatR;

namespace AccountingInventory.Application.Colors.Queries.GetColorById;

/// <summary>Used by a signup/login UI to check slug availability or display color
/// info before authentication.</summary>
public sealed record GetColorByIdQuery(Guid Id) : IRequest<ColorSummary>;

public sealed record ColorSummary(Guid Id, string Name, string Description, bool IsActive);
