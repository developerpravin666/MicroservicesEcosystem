namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record AllocateLeaveBalanceRequest(
    Guid LeaveTypeId,
    int Year,
    decimal TotalDays);
