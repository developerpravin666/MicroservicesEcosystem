using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.AllocateLeaveBalance;

public sealed record AllocateLeaveBalanceCommand(
    Guid EmployeeId,
    Guid LeaveTypeId,
    int Year,
    decimal TotalDays) : IRequest<LeaveBalanceDto>;
