using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Hr.Employees.Models;
using AccountingInventory.Domain.Entities;
using MediatR;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetEmployees;

public sealed record GetEmployeesQuery(
    string? Search = null,
    string? Department = null,
    EmploymentStatus? Status = null,
    int? Page = 1,
    int? PageSize = 50) : IRequest<PagedResult<EmployeeSummaryDto>>;
