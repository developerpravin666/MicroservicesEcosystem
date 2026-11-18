using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetSalaryStructures;

public sealed class GetSalaryStructuresQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalaryStructuresQuery, IReadOnlyList<SalaryStructureDto>>
{
    public async Task<IReadOnlyList<SalaryStructureDto>> Handle(GetSalaryStructuresQuery request, CancellationToken cancellationToken)
    {
        return await db.SalaryStructures
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.EmployeeName)
            .Select(structure => new SalaryStructureDto(
                structure.Id,
                structure.EmployeeId,
                structure.EmployeeCode,
                structure.EmployeeName,
                structure.BasicSalary,
                structure.HouseRentAllowance,
                structure.Hra,
                structure.DearnessAllowance,
                structure.ConveyanceAllowance,
                structure.SpecialAllowance,
                structure.MedicalAllowance,
                structure.PerformanceBonus,
                structure.GrossSalary,
                structure.ProvidentFund,
                structure.Esi,
                structure.ProfessionalTax,
                structure.TdsTax,
                structure.OtherDeductions,
                structure.TotalDeductions,
                structure.NetSalary,
                structure.PaymentMode,
                structure.Remarks))
            .ToListAsync(cancellationToken);
    }
}
