using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Payroll.Commands.SaveSalaryStructure;

public sealed record SaveSalaryStructureCommand(
    Guid EmployeeId,
    decimal BasicSalary,
    decimal HouseRentAllowance = 0m,
    decimal Hra = 0m,
    decimal DearnessAllowance = 0m,
    decimal ConveyanceAllowance = 0m,
    decimal SpecialAllowance = 0m,
    decimal MedicalAllowance = 0m,
    decimal PerformanceBonus = 0m,
    decimal ProvidentFund = 0m,
    decimal Esi = 0m,
    decimal ProfessionalTax = 0m,
    decimal TdsTax = 0m,
    decimal OtherDeductions = 0m,
    string PaymentMode = "Bank Transfer",
    string? Remarks = null) : IRequest<SalaryStructureDto>
{
    public decimal EffectiveHouseRentAllowance => HouseRentAllowance > 0m ? HouseRentAllowance : Hra;
}
