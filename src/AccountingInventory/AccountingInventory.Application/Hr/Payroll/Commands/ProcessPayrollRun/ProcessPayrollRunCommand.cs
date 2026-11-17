using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.ProcessPayrollRun;

public sealed record ProcessPayrollRunCommand(
    int Month,
    int Year,
    string? Notes = null,
    string? ProcessedBy = null) : IRequest<PayrollRunSummaryDto>;
