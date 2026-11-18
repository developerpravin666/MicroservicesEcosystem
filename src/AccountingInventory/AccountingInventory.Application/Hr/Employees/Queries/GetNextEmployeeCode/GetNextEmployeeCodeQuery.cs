using MediatR;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetNextEmployeeCode;

public sealed record GetNextEmployeeCodeQuery() : IRequest<string>;
