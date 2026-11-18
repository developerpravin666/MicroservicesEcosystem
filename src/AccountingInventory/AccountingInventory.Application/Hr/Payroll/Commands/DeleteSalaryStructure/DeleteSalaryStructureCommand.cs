using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DeleteSalaryStructure;

public sealed record DeleteSalaryStructureCommand(Guid EmployeeId) : IRequest<Unit>;
