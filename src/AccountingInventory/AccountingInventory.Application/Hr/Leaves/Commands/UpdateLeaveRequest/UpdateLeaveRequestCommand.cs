using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.UpdateLeaveRequest;

public sealed record UpdateLeaveRequestCommand(
    Guid Id,
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason) : IRequest<LeaveRequestDto>;
