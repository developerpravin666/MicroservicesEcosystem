using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Hr.Employees.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeSummaryDto>>
{
    public async Task<PagedResult<EmployeeSummaryDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var currentPage = request.Page.GetValueOrDefault(1) <= 0 ? 1 : request.Page.GetValueOrDefault(1);
        var currentPageSize = request.PageSize.GetValueOrDefault(50) <= 0 ? 50 : request.PageSize.GetValueOrDefault(50);

        var query = db.Employees.AsNoTracking().Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            query = query.Where(e =>
                e.EmployeeCode.ToLower().Contains(s) ||
                e.FirstName.ToLower().Contains(s) ||
                e.LastName.ToLower().Contains(s) ||
                (e.Email != null && e.Email.ToLower().Contains(s)) ||
                e.Phone.Contains(s) ||
                e.Designation.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            var dept = request.Department.Trim().ToLower();
            query = query.Where(e => e.Department != null && e.Department.ToLower() == dept);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(e => e.EmploymentStatus == request.Status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)currentPageSize);

        var items = await query
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(e => new EmployeeSummaryDto(
                e.Id,
                e.EmployeeCode,
                e.FirstName + " " + e.LastName,
                e.FirstName,
                e.LastName,
                e.Email,
                e.Phone,
                e.Department ?? "General",
                e.Designation,
                e.DateOfJoining,
                e.EmploymentType,
                e.EmploymentStatus,
                e.MonthlyBaseSalary,
                e.SalaryType ?? "Monthly",
                e.ReportsToEmployeeId,
                e.ReportsToEmployeeName,
                e.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeSummaryDto>(items, currentPage, currentPageSize, total, totalPages);
    }
}
