using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DeletePayrollRun;

public sealed record DeletePayrollRunCommand(Guid Id) : IRequest<Unit>;
