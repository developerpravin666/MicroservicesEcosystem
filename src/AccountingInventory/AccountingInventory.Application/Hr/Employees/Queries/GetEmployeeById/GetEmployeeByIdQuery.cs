using AccountingInventory.Application.Hr.Employees.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetEmployeeById;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeDetailDto>;
