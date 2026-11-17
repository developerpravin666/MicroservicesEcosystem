using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Leaves.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveTypes;

public sealed class GetLeaveTypesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetLeaveTypesQuery, IReadOnlyList<LeaveTypeDto>>
{
    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(GetLeaveTypesQuery request, CancellationToken cancellationToken)
    {
        return await db.LeaveTypes
            .AsNoTracking()
            .Where(lt => !lt.IsDeleted)
            .OrderBy(lt => lt.Name)
            .Select(lt => new LeaveTypeDto(
                lt.Id, lt.Name, lt.Code, lt.DaysAllowedPerYear, lt.IsPaid, lt.IsCarryForward, lt.Description, lt.IsActive))
            .ToListAsync(cancellationToken);
    }
}
