namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record UpdateLeaveRequestRequest(
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason);
