namespace AccountingInventory.Application.Hr.Masters.Models;

public sealed record HrMasterItemDto(
    Guid Id,
    string Category,
    string Name,
    string Code,
    string? Description,
    bool IsDefault,
    bool IsActive);
