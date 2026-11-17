namespace AccountingInventory.Application.Hr.Leaves.Models;

public sealed record LeaveBalanceDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    int Year,
    decimal TotalAllocated,
    decimal UsedDays,
    decimal PendingApprovalDays,
    decimal AvailableDays);
