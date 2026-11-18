using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.UpdateLeaveRequest;

public sealed class UpdateLeaveRequestCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<UpdateLeaveRequestCommandHandler> logger)
    : IRequestHandler<UpdateLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(UpdateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Leave request with ID '{request.Id}' was not found.");

        if (leave.Status != LeaveRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending leave applications can be modified.");
        }

        var leaveType = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Leave type '{request.LeaveTypeId}' was not found.");

        // If days or category changed, update pending balance
        if (leave.LeaveTypeId != request.LeaveTypeId || leave.TotalDays != request.TotalDays)
        {
            var oldBalance = await db.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeId == leave.EmployeeId && b.LeaveTypeId == leave.LeaveTypeId && b.Year == leave.StartDate.Year && !b.IsDeleted,
                cancellationToken);
            oldBalance?.RemovePending(leave.TotalDays);

            var newBalance = await db.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeId == leave.EmployeeId && b.LeaveTypeId == request.LeaveTypeId && b.Year == request.StartDate.Year && !b.IsDeleted,
                cancellationToken);
            newBalance?.AddPending(request.TotalDays);
        }

        leave.Update(
            request.LeaveTypeId,
            leaveType.Name,
            request.StartDate,
            request.EndDate,
            request.TotalDays,
            request.Reason);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Leave request {Id} updated.", leave.Id);

        return new LeaveRequestDto(
            leave.Id,
            leave.EmployeeId,
            leave.EmployeeCode,
            leave.EmployeeName,
            leave.LeaveTypeId,
            leave.LeaveTypeName,
            leave.StartDate,
            leave.EndDate,
            leave.TotalDays,
            leave.Reason,
            leave.Status,
            leave.ApproverComment,
            leave.ApprovedBy,
            leave.ApprovedAt,
            leave.CreatedAt);
    }
}
