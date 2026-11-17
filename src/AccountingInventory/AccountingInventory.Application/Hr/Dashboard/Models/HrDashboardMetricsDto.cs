namespace AccountingInventory.Application.Hr.Dashboard.Models;

public sealed record HrDashboardMetricsDto(
    int TotalEmployees,
    int ActiveEmployees,
    int OnLeaveEmployees,
    int TotalDepartments,
    int PendingLeaveRequests,
    decimal TotalMonthlyPayroll,
    int CompletedPayrollRuns);
