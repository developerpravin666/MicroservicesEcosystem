using AccountingInventory.Application.Hr.Departments.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Departments.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(
    string Name,
    string Code,
    string? Description,
    string? HeadOfDepartment) : IRequest<DepartmentDto>;
