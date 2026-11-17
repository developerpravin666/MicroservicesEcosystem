namespace AccountingInventory.Application.Hr.Leaves.Models;

public sealed record LeaveTypeDto(
    Guid Id,
    string Name,
    string Code,
    int DaysAllowedPerYear,
    bool IsPaid,
    bool IsCarryForward,
    string? Description,
    bool IsActive);
