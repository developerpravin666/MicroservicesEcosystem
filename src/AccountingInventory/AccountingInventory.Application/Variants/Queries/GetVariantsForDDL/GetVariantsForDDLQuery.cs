using MediatR;

namespace AccountingInventory.Application.Variants.Queries.GetVariantsForDDL;

public sealed record GetVariantsForDDL() : IRequest<IEnumerable<GetVariantsForDDLSummary>>;

public sealed record GetVariantsForDDLSummary(Guid Id, string Name);

