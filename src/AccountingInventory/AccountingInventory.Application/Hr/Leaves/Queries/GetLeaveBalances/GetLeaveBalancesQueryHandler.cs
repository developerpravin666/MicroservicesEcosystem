using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveBalances;

public sealed class GetLeaveBalancesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetLeaveBalancesQuery, IReadOnlyList<LeaveBalanceDto>>
{
    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetLeaveBalancesQuery request, CancellationToken cancellationToken)
    {
        var targetYear = request.Year.GetValueOrDefault(DateTime.UtcNow.Year);
        var query = db.LeaveBalances.AsNoTracking().Where(lb => !lb.IsDeleted && lb.Year == targetYear);

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(lb => lb.EmployeeId == request.EmployeeId.Value);
        }

        return await query
            .OrderBy(lb => lb.EmployeeName)
            .ThenBy(lb => lb.LeaveTypeName)
            .Select(lb => new LeaveBalanceDto(
                lb.Id, lb.EmployeeId, lb.EmployeeCode, lb.EmployeeName, lb.LeaveTypeId,
                lb.LeaveTypeName, lb.Year, lb.TotalAllocated, lb.UsedDays, lb.PendingApprovalDays,
                lb.AvailableDays))
            .ToListAsync(cancellationToken);
    }
}
