using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DisbursePayrollRun;

public sealed record DisbursePayrollRunCommand(
    Guid RunId,
    DateOnly PaymentDate,
    string? Notes = null) : IRequest<PayrollRunSummaryDto>;
