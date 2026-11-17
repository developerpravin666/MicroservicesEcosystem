using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetPayrollRuns;

public sealed record GetPayrollRunsQuery(int? Year = null) : IRequest<IReadOnlyList<PayrollRunSummaryDto>>;
