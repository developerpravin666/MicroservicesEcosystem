using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetPayslipsByRun;

public sealed record GetPayslipsByRunQuery(Guid RunId) : IRequest<IReadOnlyList<PayslipDto>>;
