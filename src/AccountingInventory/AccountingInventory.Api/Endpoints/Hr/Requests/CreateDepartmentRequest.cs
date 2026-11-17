namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record CreateDepartmentRequest(
    string Name,
    string Code,
    string? Description,
    string? HeadOfDepartment);
