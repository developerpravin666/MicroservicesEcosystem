namespace AccountingInventory.Application.Hr.Payroll.Models;

public sealed record StatutoryCalculationResult(
    decimal GrossMonthlySalary,
    decimal BasicSalary,
    decimal Hra,
    decimal ConveyanceAllowance,
    decimal MedicalAllowance,
    decimal SpecialAllowance,
    decimal PerformanceBonus,
    decimal ProvidentFundEmployee,
    decimal ProvidentFundEmployer,
    decimal EsiEmployee,
    decimal EsiEmployer,
    decimal ProfessionalTax,
    decimal TdsTax,
    decimal TotalDeductions,
    decimal NetMonthlySalary,
    decimal AnnualCtc);

public static class IndianPayrollCalculator
{
    public static StatutoryCalculationResult Calculate(decimal monthlySalary, bool isCtc = false)
    {
        var grossMonthly = isCtc ? Math.Round(monthlySalary / 12m, 2) : monthlySalary;
        grossMonthly = Math.Max(0m, grossMonthly);

        // Standard Indian Corporate Structure:
        // Basic: 50% of Gross
        var basic = Math.Round(grossMonthly * 0.50m, 2);
        // HRA: 20% of Gross (or 40-50% of Basic)
        var hra = Math.Round(grossMonthly * 0.20m, 2);
        // Conveyance: Standard fixed ₹1,600 (or up to remainder)
        var conveyance = Math.Min(1600m, Math.Max(0m, grossMonthly - basic - hra));
        // Medical Allowance: Standard ₹1,250
        var medical = Math.Min(1250m, Math.Max(0m, grossMonthly - basic - hra - conveyance));
        // Special Allowance: Balancing figure
        var special = Math.Max(0m, grossMonthly - (basic + hra + conveyance + medical));
        var bonus = 0m;

        // Provident Fund: 12% of Basic up to statutory ceiling (₹15,000 threshold or full basic)
        // Standard: 12% of Basic capped at ₹1,800 or actual
        decimal pfEmployee = 0m;
        decimal pfEmployer = 0m;
        if (basic > 0)
        {
            var pfWages = Math.Min(basic, 15000m); // Statutory ceiling ₹15,000
            pfEmployee = Math.Round(pfWages * 0.12m, 2);
            pfEmployer = pfEmployee;
        }

        // ESIC: If Gross <= ₹21,000, Employee 0.75%, Employer 3.25%
        decimal esiEmployee = 0m;
        decimal esiEmployer = 0m;
        if (grossMonthly <= 21000m && grossMonthly > 0)
        {
            esiEmployee = Math.Round(grossMonthly * 0.0075m, 2);
            esiEmployer = Math.Round(grossMonthly * 0.0325m, 2);
        }

        // Professional Tax (Standard Maharashtra / State slab):
        // Up to ₹7,500: ₹0
        // ₹7,501 to ₹10,000: ₹175
        // Above ₹10,000: ₹200 (₹300 in Feb)
        decimal professionalTax = 0m;
        if (grossMonthly > 10000m)
        {
            professionalTax = 200m;
        }
        else if (grossMonthly > 7500m)
        {
            professionalTax = 175m;
        }

        // TDS (Income Tax withholding estimation under New Tax Regime FY 2024-25 / 2025-26):
        // Annual taxable gross (Gross * 12) - Standard Deduction ₹75,000
        var annualGross = grossMonthly * 12m;
        var annualTaxable = Math.Max(0m, annualGross - 75000m);
        decimal annualTax = 0m;
        if (annualTaxable > 700000m) // Rebate under Section 87A up to 7 Lakhs (Nil tax)
        {
            if (annualTaxable > 1500000m)
            {
                annualTax += (annualTaxable - 1500000m) * 0.30m + 150000m;
            }
            else if (annualTaxable > 1200000m)
            {
                annualTax += (annualTaxable - 1200000m) * 0.20m + 90000m;
            }
            else if (annualTaxable > 1000000m)
            {
                annualTax += (annualTaxable - 1000000m) * 0.15m + 60000m;
            }
            else if (annualTaxable > 700000m)
            {
                annualTax += (annualTaxable - 700000m) * 0.10m + 30000m;
            }
        }
        var tdsTax = Math.Round(annualTax / 12m, 2);

        var totalDeductions = pfEmployee + esiEmployee + professionalTax + tdsTax;
        var netMonthly = Math.Max(0m, grossMonthly - totalDeductions);
        var annualCtc = (grossMonthly * 12m) + (pfEmployer * 12m) + (esiEmployer * 12m);

        return new StatutoryCalculationResult(
            GrossMonthlySalary: grossMonthly,
            BasicSalary: basic,
            Hra: hra,
            ConveyanceAllowance: conveyance,
            MedicalAllowance: medical,
            SpecialAllowance: special,
            PerformanceBonus: bonus,
            ProvidentFundEmployee: pfEmployee,
            ProvidentFundEmployer: pfEmployer,
            EsiEmployee: esiEmployee,
            EsiEmployer: esiEmployer,
            ProfessionalTax: professionalTax,
            TdsTax: tdsTax,
            TotalDeductions: totalDeductions,
            NetMonthlySalary: netMonthly,
            AnnualCtc: annualCtc);
    }
}
