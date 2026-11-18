using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.DeleteLeaveRequest;

public sealed class DeleteLeaveRequestCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DeleteLeaveRequestCommandHandler> logger)
    : IRequestHandler<DeleteLeaveRequestCommand, Unit>
{
    public async Task<Unit> Handle(DeleteLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Leave request with ID '{request.Id}' was not found.");

        if (leave.Status == LeaveRequestStatus.Pending)
        {
            var balance = await db.LeaveBalances.FirstOrDefaultAsync(
                b => b.EmployeeId == leave.EmployeeId && b.LeaveTypeId == leave.LeaveTypeId && b.Year == leave.StartDate.Year && !b.IsDeleted,
                cancellationToken);

            balance?.RemovePending(leave.TotalDays);
        }

        leave.Delete();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Leave request {Id} for employee {EmployeeId} deleted.", leave.Id, leave.EmployeeId);
        return Unit.Value;
    }
}
