using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetSalaryStructure;

public sealed class GetSalaryStructureQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalaryStructureQuery, SalaryStructureDto?>
{
    public async Task<SalaryStructureDto?> Handle(GetSalaryStructureQuery request, CancellationToken cancellationToken)
    {
        var structure = await db.SalaryStructures
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == request.EmployeeId && !s.IsDeleted, cancellationToken);

        if (structure is null) return null;

        return new SalaryStructureDto(
            structure.Id, structure.EmployeeId, structure.EmployeeCode, structure.EmployeeName,
            structure.BasicSalary, structure.Hra, structure.DearnessAllowance, structure.ConveyanceAllowance,
            structure.SpecialAllowance, structure.MedicalAllowance, structure.PerformanceBonus,
            structure.GrossSalary, structure.ProvidentFund, structure.Esi, structure.ProfessionalTax,
            structure.TdsTax, structure.OtherDeductions, structure.TotalDeductions, structure.NetSalary,
            structure.PaymentMode, structure.Remarks);
    }
}
