using AccountingInventory.Application.Hr.Masters.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Masters.Queries.GetMasters;

public sealed record GetHrMastersQuery(string? Category = null) : IRequest<IReadOnlyList<HrMasterItemDto>>;
