using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveRequests;

public sealed class GetLeaveRequestsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = db.LeaveRequests.AsNoTracking().Where(lr => !lr.IsDeleted);

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(lr => lr.EmployeeId == request.EmployeeId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(lr => lr.Status == request.Status.Value);
        }

        return await query
            .OrderByDescending(lr => lr.CreatedAt)
            .Select(lr => new LeaveRequestDto(
                lr.Id, lr.EmployeeId, lr.EmployeeCode, lr.EmployeeName, lr.LeaveTypeId,
                lr.LeaveTypeName, lr.StartDate, lr.EndDate, lr.TotalDays, lr.Reason,
                lr.Status, lr.ApproverComment, lr.ApprovedBy, lr.ApprovedAt, lr.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
