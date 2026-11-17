using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Departments.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Departments.Queries.GetDepartments;

public sealed class GetDepartmentsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        return await db.Departments
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.Description, d.HeadOfDepartment, d.IsActive))
            .ToListAsync(cancellationToken);
    }
}
