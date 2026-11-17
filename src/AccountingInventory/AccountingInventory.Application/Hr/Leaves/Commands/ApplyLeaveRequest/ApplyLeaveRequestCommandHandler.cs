using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.ApplyLeaveRequest;

public sealed class ApplyLeaveRequestCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<ApplyLeaveRequestCommandHandler> logger)
    : IRequestHandler<ApplyLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(ApplyLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.EmployeeId}' was not found.");

        var leaveType = await db.LeaveTypes.FirstOrDefaultAsync(lt => lt.Id == request.LeaveTypeId && !lt.IsDeleted, cancellationToken);
        if (leaveType is null)
            throw new NotFoundException($"Leave type with id '{request.LeaveTypeId}' was not found.");

        var currentYear = request.StartDate.Year;
        var balance = await db.LeaveBalances.FirstOrDefaultAsync(
            lb => lb.EmployeeId == request.EmployeeId && lb.LeaveTypeId == request.LeaveTypeId && lb.Year == currentYear && !lb.IsDeleted,
            cancellationToken);

        if (balance is null)
        {
            balance = LeaveBalance.Create(
                emp.Id,
                emp.EmployeeCode,
                $"{emp.FirstName} {emp.LastName}".Trim(),
                leaveType.Id,
                leaveType.Name,
                currentYear,
                leaveType.DaysAllowedPerYear);
            db.LeaveBalances.Add(balance);
        }

        var (netWorkingDays, weekends, holidays) = LeaveWorkingDaysCalculator.CalculateNetLeaveDays(request.StartDate, request.EndDate);
        if (netWorkingDays <= 0)
        {
            throw new BadRequestException("Selected date range only consists of weekends or statutory holidays. No leave request is needed.");
        }

        var requestedDays = netWorkingDays;

        if (leaveType.IsPaid && balance.AvailableDays < requestedDays)
        {
            throw new ConflictException($"Insufficient leave balance. Available: {balance.AvailableDays} days, Requested working days: {requestedDays} days (excluded {weekends} weekend days and {holidays} holidays).");
        }

        var leaveReq = LeaveRequest.Create(
            emp.Id,
            emp.EmployeeCode,
            $"{emp.FirstName} {emp.LastName}".Trim(),
            leaveType.Id,
            leaveType.Name,
            request.StartDate,
            request.EndDate,
            requestedDays,
            request.Reason);

        balance.AddPending(requestedDays);
        db.LeaveRequests.Add(leaveReq);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Leave application created for {EmpCode} ({Days} days).", emp.EmployeeCode, request.TotalDays);

        return new LeaveRequestDto(
            leaveReq.Id, leaveReq.EmployeeId, leaveReq.EmployeeCode, leaveReq.EmployeeName,
            leaveReq.LeaveTypeId, leaveReq.LeaveTypeName, leaveReq.StartDate, leaveReq.EndDate,
            leaveReq.TotalDays, leaveReq.Reason, leaveReq.Status, leaveReq.ApproverComment,
            leaveReq.ApprovedBy, leaveReq.ApprovedAt, leaveReq.CreatedAt);
    }
}
