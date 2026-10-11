using MediatR;

namespace AccountingInventory.Application.Colors.Queries.GetColorsForDDL;

public sealed record GetColorsForDDL() : IRequest<IEnumerable<GetColorsForDDLSummary>>;

public sealed record GetColorsForDDLSummary(Guid Id, string Name);

