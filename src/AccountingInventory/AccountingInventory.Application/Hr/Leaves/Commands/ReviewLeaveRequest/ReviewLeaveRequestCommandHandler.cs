using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.ReviewLeaveRequest;

public sealed class ReviewLeaveRequestCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<ReviewLeaveRequestCommandHandler> logger)
    : IRequestHandler<ReviewLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(ReviewLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leaveReq = await db.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == request.RequestId && !lr.IsDeleted, cancellationToken);
        if (leaveReq is null)
            throw new NotFoundException($"Leave request with id '{request.RequestId}' was not found.");

        if (leaveReq.Status != LeaveRequestStatus.Pending)
            throw new ConflictException("Leave request is already processed and cannot be reviewed again.");

        var balance = await db.LeaveBalances.FirstOrDefaultAsync(
            lb => lb.EmployeeId == leaveReq.EmployeeId && lb.LeaveTypeId == leaveReq.LeaveTypeId && lb.Year == leaveReq.StartDate.Year && !lb.IsDeleted,
            cancellationToken);

        if (request.IsApproved)
        {
            leaveReq.Approve(request.Approver, request.Comment);
            balance?.Consume(leaveReq.TotalDays);
            logger.LogInformation("Leave request {Id} for employee {Emp} APPROVED by {Approver}.", leaveReq.Id, leaveReq.EmployeeCode, request.Approver);
        }
        else
        {
            leaveReq.Reject(request.Approver, request.Comment);
            balance?.RemovePending(leaveReq.TotalDays);
            logger.LogInformation("Leave request {Id} for employee {Emp} REJECTED by {Approver}.", leaveReq.Id, leaveReq.EmployeeCode, request.Approver);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new LeaveRequestDto(
            leaveReq.Id, leaveReq.EmployeeId, leaveReq.EmployeeCode, leaveReq.EmployeeName,
            leaveReq.LeaveTypeId, leaveReq.LeaveTypeName, leaveReq.StartDate, leaveReq.EndDate,
            leaveReq.TotalDays, leaveReq.Reason, leaveReq.Status, leaveReq.ApproverComment,
            leaveReq.ApprovedBy, leaveReq.ApprovedAt, leaveReq.CreatedAt);
    }
}
