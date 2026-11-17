namespace AccountingInventory.Application.Hr.Departments.Models;

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? HeadOfDepartment,
    bool IsActive);
