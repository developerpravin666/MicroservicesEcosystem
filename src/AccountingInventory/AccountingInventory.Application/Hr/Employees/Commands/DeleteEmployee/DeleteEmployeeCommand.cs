using MediatR;

namespace AccountingInventory.Application.Hr.Employees.Commands.DeleteEmployee;

public sealed record DeleteEmployeeCommand(Guid Id) : IRequest;
