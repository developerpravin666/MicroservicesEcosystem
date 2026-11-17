using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveTypes;

public sealed record GetLeaveTypesQuery : IRequest<IReadOnlyList<LeaveTypeDto>>;
