using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetSalaryStructure;

public sealed record GetSalaryStructureQuery(Guid EmployeeId) : IRequest<SalaryStructureDto?>;
