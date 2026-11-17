using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.CreateLeaveType;

public sealed record CreateLeaveTypeCommand(
    string Name,
    string Code,
    int DaysAllowedPerYear = 12,
    bool IsPaid = true,
    bool IsCarryForward = false,
    string? Description = null) : IRequest<LeaveTypeDto>;
