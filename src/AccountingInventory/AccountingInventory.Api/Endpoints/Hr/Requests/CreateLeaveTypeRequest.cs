namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record CreateLeaveTypeRequest(
    string Name,
    string Code,
    int DaysAllowedPerYear,
    bool IsPaid,
    bool IsCarryForward,
    string? Description);
