using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveBalances;

public sealed record GetLeaveBalancesQuery(Guid? EmployeeId = null, int? Year = null) : IRequest<IReadOnlyList<LeaveBalanceDto>>;
