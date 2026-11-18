using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetSalaryStructures;

public sealed record GetSalaryStructuresQuery : IRequest<IReadOnlyList<SalaryStructureDto>>;
