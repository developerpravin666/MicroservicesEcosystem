using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.ApplyLeaveRequest;

public sealed record ApplyLeaveRequestCommand(
    Guid EmployeeId,
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason) : IRequest<LeaveRequestDto>;
