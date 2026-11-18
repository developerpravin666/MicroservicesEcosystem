namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record SaveSalaryStructureRequest(
    decimal BasicSalary,
    decimal? HouseRentAllowance = null,
    decimal? Hra = null,
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
    string? Remarks = null,
    Guid? EmployeeId = null)
{
    public decimal EffectiveHouseRentAllowance => (HouseRentAllowance.HasValue && HouseRentAllowance.Value > 0m)
        ? HouseRentAllowance.Value
        : (Hra ?? 0m);
}
