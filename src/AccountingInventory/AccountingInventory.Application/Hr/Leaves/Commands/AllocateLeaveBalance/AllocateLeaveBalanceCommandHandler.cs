using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Leaves.Commands.AllocateLeaveBalance;

public sealed class AllocateLeaveBalanceCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<AllocateLeaveBalanceCommandHandler> logger)
    : IRequestHandler<AllocateLeaveBalanceCommand, LeaveBalanceDto>
{
    public async Task<LeaveBalanceDto> Handle(AllocateLeaveBalanceCommand request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.EmployeeId}' was not found.");

        var leaveType = await db.LeaveTypes.FirstOrDefaultAsync(lt => lt.Id == request.LeaveTypeId && !lt.IsDeleted, cancellationToken);
        if (leaveType is null)
            throw new NotFoundException($"Leave type with id '{request.LeaveTypeId}' was not found.");

        var existing = await db.LeaveBalances.FirstOrDefaultAsync(
            lb => lb.EmployeeId == request.EmployeeId && lb.LeaveTypeId == request.LeaveTypeId && lb.Year == request.Year && !lb.IsDeleted,
            cancellationToken);

        if (existing is not null)
        {
            existing.AdjustAllocated(request.TotalDays);
            await db.SaveChangesAsync(cancellationToken);
            return new LeaveBalanceDto(
                existing.Id, existing.EmployeeId, existing.EmployeeCode, existing.EmployeeName,
                existing.LeaveTypeId, existing.LeaveTypeName, existing.Year, existing.TotalAllocated,
                existing.UsedDays, existing.PendingApprovalDays, existing.AvailableDays);
        }

        var balance = LeaveBalance.Create(
            emp.Id,
            emp.EmployeeCode,
            $"{emp.FirstName} {emp.LastName}".Trim(),
            leaveType.Id,
            leaveType.Name,
            request.Year,
            request.TotalDays);

        db.LeaveBalances.Add(balance);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Allocated {Days} days of {Leave} to {Employee} for year {Year}.", request.TotalDays, leaveType.Name, emp.EmployeeCode, request.Year);

        return new LeaveBalanceDto(
            balance.Id, balance.EmployeeId, balance.EmployeeCode, balance.EmployeeName,
            balance.LeaveTypeId, balance.LeaveTypeName, balance.Year, balance.TotalAllocated,
            balance.UsedDays, balance.PendingApprovalDays, balance.AvailableDays);
    }
}
