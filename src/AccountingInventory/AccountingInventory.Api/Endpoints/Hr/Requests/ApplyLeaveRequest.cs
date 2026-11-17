namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record ApplyLeaveRequest(
    Guid EmployeeId,
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason);
