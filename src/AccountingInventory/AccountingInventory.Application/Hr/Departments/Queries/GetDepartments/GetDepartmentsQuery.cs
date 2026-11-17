using AccountingInventory.Application.Hr.Departments.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Departments.Queries.GetDepartments;

public sealed record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;
