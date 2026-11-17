namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record SaveSalaryStructureRequest(
    decimal BasicSalary,
    decimal Hra,
    decimal DearnessAllowance,
    decimal ConveyanceAllowance,
    decimal SpecialAllowance,
    decimal MedicalAllowance,
    decimal PerformanceBonus,
    decimal ProvidentFund,
    decimal Esi,
    decimal ProfessionalTax,
    decimal TdsTax,
    decimal OtherDeductions,
    string PaymentMode,
    string? Remarks);
