using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.SaveSalaryStructure;

public sealed class SaveSalaryStructureCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<SaveSalaryStructureCommandHandler> logger)
    : IRequestHandler<SaveSalaryStructureCommand, SalaryStructureDto>
{
    public async Task<SalaryStructureDto> Handle(SaveSalaryStructureCommand request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.EmployeeId}' was not found.");

        var existing = await db.SalaryStructures.FirstOrDefaultAsync(s => s.EmployeeId == request.EmployeeId && !s.IsDeleted, cancellationToken);
        SalaryStructure structure;

        var effectiveHra = request.EffectiveHouseRentAllowance;

        if (existing is not null)
        {
            existing.Update(
                request.BasicSalary, effectiveHra, request.DearnessAllowance, request.ConveyanceAllowance,
                request.SpecialAllowance, request.MedicalAllowance, request.PerformanceBonus,
                request.ProvidentFund, request.Esi, request.ProfessionalTax, request.TdsTax,
                request.OtherDeductions, request.PaymentMode, request.Remarks, effectiveHra);
            structure = existing;
            logger.LogInformation("Salary structure for employee {EmpCode} updated.", emp.EmployeeCode);
        }
        else
        {
            structure = SalaryStructure.Create(
                emp.Id, emp.EmployeeCode, $"{emp.FirstName} {emp.LastName}".Trim(),
                request.BasicSalary, effectiveHra, request.DearnessAllowance, request.ConveyanceAllowance,
                request.SpecialAllowance, request.MedicalAllowance, request.PerformanceBonus,
                request.ProvidentFund, request.Esi, request.ProfessionalTax, request.TdsTax,
                request.OtherDeductions, request.PaymentMode, request.Remarks, effectiveHra);
            db.SalaryStructures.Add(structure);
            logger.LogInformation("Salary structure for employee {EmpCode} created.", emp.EmployeeCode);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new SalaryStructureDto(
            structure.Id, structure.EmployeeId, structure.EmployeeCode, structure.EmployeeName,
            structure.BasicSalary, structure.HouseRentAllowance, structure.Hra, structure.DearnessAllowance, structure.ConveyanceAllowance,
            structure.SpecialAllowance, structure.MedicalAllowance, structure.PerformanceBonus,
            structure.GrossSalary, structure.ProvidentFund, structure.Esi, structure.ProfessionalTax,
            structure.TdsTax, structure.OtherDeductions, structure.TotalDeductions, structure.NetSalary,
            structure.PaymentMode, structure.Remarks);
    }
}
