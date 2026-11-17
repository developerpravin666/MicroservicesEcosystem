using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Dashboard.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Dashboard.Queries.GetHrDashboardMetrics;

public sealed class GetHrDashboardMetricsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetHrDashboardMetricsQuery, HrDashboardMetricsDto>
{
    public async Task<HrDashboardMetricsDto> Handle(GetHrDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var totalEmployees = await db.Employees.CountAsync(e => !e.IsDeleted, cancellationToken);
        var activeEmployees = await db.Employees.CountAsync(e => !e.IsDeleted && (e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.OnProbation), cancellationToken);
        var onLeaveEmployees = await db.Employees.CountAsync(e => !e.IsDeleted && e.EmploymentStatus == EmploymentStatus.OnLeave, cancellationToken);
        var totalDepartments = await db.Departments.CountAsync(d => !d.IsDeleted, cancellationToken);
        var pendingLeaves = await db.LeaveRequests.CountAsync(lr => !lr.IsDeleted && lr.Status == LeaveRequestStatus.Pending, cancellationToken);

        var totalMonthlyPayroll = await db.Employees
            .Where(e => !e.IsDeleted && (e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.OnProbation))
            .SumAsync(e => (decimal?)e.MonthlyBaseSalary, cancellationToken) ?? 0m;

        var completedRuns = await db.PayrollRuns.CountAsync(p => !p.IsDeleted && (p.Status == PayrollRunStatus.Paid || p.Status == PayrollRunStatus.Approved), cancellationToken);

        return new HrDashboardMetricsDto(
            totalEmployees,
            activeEmployees,
            onLeaveEmployees,
            totalDepartments,
            pendingLeaves,
            totalMonthlyPayroll,
            completedRuns);
    }
}
