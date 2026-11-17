using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.ReviewLeaveRequest;

public sealed record ReviewLeaveRequestCommand(
    Guid RequestId,
    bool IsApproved,
    string Approver,
    string? Comment = null) : IRequest<LeaveRequestDto>;
