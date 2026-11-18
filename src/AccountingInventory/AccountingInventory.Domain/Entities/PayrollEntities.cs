using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum PayrollRunStatus
{
    Draft = 1,
    Processed = 2,
    Approved = 3,
    Paid = 4,
    Cancelled = 5
}

public enum PayslipPaymentStatus
{
    Pending = 1,
    Paid = 2
}

/// <summary>
/// Breakdown of earnings, statutory deductions, and allowances for an employee.
/// </summary>
public sealed class SalaryStructure : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public string EmployeeCode { get; private set; } = null!;
    public string EmployeeName { get; private set; } = null!;
    
    // Earnings
    public decimal BasicSalary { get; private set; } = 0m;
    public decimal Hra { get; private set; } = 0m;
    public decimal DearnessAllowance { get; private set; } = 0m;
    public decimal ConveyanceAllowance { get; private set; } = 0m;
    public decimal SpecialAllowance { get; private set; } = 0m;
    public decimal MedicalAllowance { get; private set; } = 0m;
    public decimal PerformanceBonus { get; private set; } = 0m;
    public decimal GrossSalary => BasicSalary + Hra + DearnessAllowance + ConveyanceAllowance + SpecialAllowance + MedicalAllowance + PerformanceBonus;

    // Deductions
    public decimal ProvidentFund { get; private set; } = 0m;
    public decimal Esi { get; private set; } = 0m;
    public decimal ProfessionalTax { get; private set; } = 0m;
    public decimal TdsTax { get; private set; } = 0m;
    public decimal OtherDeductions { get; private set; } = 0m;
    public decimal TotalDeductions => ProvidentFund + Esi + ProfessionalTax + TdsTax + OtherDeductions;

    public decimal NetSalary => Math.Max(0m, GrossSalary - TotalDeductions);
    public string PaymentMode { get; private set; } = "Bank Transfer";
    public string? Remarks { get; private set; }

    public static SalaryStructure Create(
        Guid employeeId,
        string employeeCode,
        string employeeName,
        decimal basicSalary,
        decimal hra = 0m,
        decimal dearnessAllowance = 0m,
        decimal conveyanceAllowance = 0m,
        decimal specialAllowance = 0m,
        decimal medicalAllowance = 0m,
        decimal performanceBonus = 0m,
        decimal providentFund = 0m,
        decimal esi = 0m,
        decimal professionalTax = 0m,
        decimal tdsTax = 0m,
        decimal otherDeductions = 0m,
        string paymentMode = "Bank Transfer",
        string? remarks = null)
    {
        return new SalaryStructure
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            EmployeeName = employeeName.Trim(),
            BasicSalary = Math.Max(0m, basicSalary),
            Hra = Math.Max(0m, hra),
            DearnessAllowance = Math.Max(0m, dearnessAllowance),
            ConveyanceAllowance = Math.Max(0m, conveyanceAllowance),
            SpecialAllowance = Math.Max(0m, specialAllowance),
            MedicalAllowance = Math.Max(0m, medicalAllowance),
            PerformanceBonus = Math.Max(0m, performanceBonus),
            ProvidentFund = Math.Max(0m, providentFund),
            Esi = Math.Max(0m, esi),
            ProfessionalTax = Math.Max(0m, professionalTax),
            TdsTax = Math.Max(0m, tdsTax),
            OtherDeductions = Math.Max(0m, otherDeductions),
            PaymentMode = string.IsNullOrWhiteSpace(paymentMode) ? "Bank Transfer" : paymentMode.Trim(),
            Remarks = remarks?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(
        decimal basicSalary,
        decimal hra,
        decimal dearnessAllowance,
        decimal conveyanceAllowance,
        decimal specialAllowance,
        decimal medicalAllowance,
        decimal performanceBonus,
        decimal providentFund,
        decimal esi,
        decimal professionalTax,
        decimal tdsTax,
        decimal otherDeductions,
        string paymentMode = "Bank Transfer",
        string? remarks = null)
    {
        BasicSalary = Math.Max(0m, basicSalary);
        Hra = Math.Max(0m, hra);
        DearnessAllowance = Math.Max(0m, dearnessAllowance);
        ConveyanceAllowance = Math.Max(0m, conveyanceAllowance);
        SpecialAllowance = Math.Max(0m, specialAllowance);
        MedicalAllowance = Math.Max(0m, medicalAllowance);
        PerformanceBonus = Math.Max(0m, performanceBonus);
        ProvidentFund = Math.Max(0m, providentFund);
        Esi = Math.Max(0m, esi);
        ProfessionalTax = Math.Max(0m, professionalTax);
        TdsTax = Math.Max(0m, tdsTax);
        OtherDeductions = Math.Max(0m, otherDeductions);
        PaymentMode = string.IsNullOrWhiteSpace(paymentMode) ? "Bank Transfer" : paymentMode.Trim();
        Remarks = remarks?.Trim();
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Monthly payroll execution batch run for a company/tenant.
/// </summary>
public sealed class PayrollRun : AggregateRoot
{
    public int Month { get; private set; } // 1 to 12
    public int Year { get; private set; }
    public DateOnly RunDate { get; private set; }
    public int TotalEmployees { get; private set; }
    public decimal TotalGrossAmount { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal TotalNetAmount { get; private set; }
    public PayrollRunStatus Status { get; private set; } = PayrollRunStatus.Draft;
    public DateOnly? PaymentDate { get; private set; }
    public string? ProcessedBy { get; private set; }
    public string? Notes { get; private set; }

    public static PayrollRun Create(
        int month,
        int year,
        DateOnly runDate,
        int totalEmployees,
        decimal totalGrossAmount,
        decimal totalDeductions,
        decimal totalNetAmount,
        string? processedBy = null,
        string? notes = null)
    {
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        return new PayrollRun
        {
            Id = Guid.NewGuid(),
            Month = month,
            Year = year,
            RunDate = runDate,
            TotalEmployees = totalEmployees,
            TotalGrossAmount = totalGrossAmount,
            TotalDeductions = totalDeductions,
            TotalNetAmount = totalNetAmount,
            Status = PayrollRunStatus.Processed,
            ProcessedBy = processedBy?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Approve()
    {
        Status = PayrollRunStatus.Approved;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPaid(DateOnly paymentDate)
    {
        Status = PayrollRunStatus.Paid;
        PaymentDate = paymentDate;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = PayrollRunStatus.Cancelled;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        Status = PayrollRunStatus.Cancelled;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Individual employee monthly salary receipt / payslip voucher.
/// </summary>
public sealed class Payslip : AggregateRoot
{
    public Guid PayrollRunId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string EmployeeCode { get; private set; } = null!;
    public string EmployeeName { get; private set; } = null!;
    public string? Department { get; private set; }
    public string Designation { get; private set; } = null!;
    public int Month { get; private set; }
    public int Year { get; private set; }
    
    // Attendance & Days
    public int WorkingDays { get; private set; } = 30;
    public int PresentDays { get; private set; } = 30;
    public decimal PaidLeaveDays { get; private set; } = 0m;
    public decimal UnpaidLeaveDays { get; private set; } = 0m;

    // Computed Earnings
    public decimal BasicSalary { get; private set; }
    public decimal Hra { get; private set; }
    public decimal Allowances { get; private set; }
    public decimal Bonus { get; private set; }
    public decimal GrossEarnings { get; private set; }

    // Computed Deductions
    public decimal PfDeduction { get; private set; }
    public decimal EsiDeduction { get; private set; }
    public decimal ProfessionalTax { get; private set; }
    public decimal TdsDeduction { get; private set; }
    public decimal LopDeduction { get; private set; }
    public decimal OtherDeductions { get; private set; }
    public decimal TotalDeductions { get; private set; }

    // Final Net Pay
    public decimal NetSalary { get; private set; }
    public PayslipPaymentStatus PaymentStatus { get; private set; } = PayslipPaymentStatus.Pending;
    public DateTimeOffset? DisbursedAt { get; private set; }
    public string? PaymentReference { get; private set; }

    public static Payslip Create(
        Guid payrollRunId,
        Guid employeeId,
        string employeeCode,
        string employeeName,
        string? department,
        string designation,
        int month,
        int year,
        int workingDays,
        int presentDays,
        decimal paidLeaveDays,
        decimal unpaidLeaveDays,
        decimal basicSalary,
        decimal hra,
        decimal allowances,
        decimal bonus,
        decimal pfDeduction,
        decimal esiDeduction,
        decimal professionalTax,
        decimal tdsDeduction,
        decimal lopDeduction,
        decimal otherDeductions)
    {
        var gross = basicSalary + hra + allowances + bonus;
        var totalDeductions = pfDeduction + esiDeduction + professionalTax + tdsDeduction + lopDeduction + otherDeductions;
        var net = Math.Max(0m, gross - totalDeductions);

        return new Payslip
        {
            Id = Guid.NewGuid(),
            PayrollRunId = payrollRunId,
            EmployeeId = employeeId,
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            EmployeeName = employeeName.Trim(),
            Department = department?.Trim(),
            Designation = designation.Trim(),
            Month = month,
            Year = year,
            WorkingDays = workingDays,
            PresentDays = presentDays,
            PaidLeaveDays = paidLeaveDays,
            UnpaidLeaveDays = unpaidLeaveDays,
            BasicSalary = basicSalary,
            Hra = hra,
            Allowances = allowances,
            Bonus = bonus,
            GrossEarnings = gross,
            PfDeduction = pfDeduction,
            EsiDeduction = esiDeduction,
            ProfessionalTax = professionalTax,
            TdsDeduction = tdsDeduction,
            LopDeduction = lopDeduction,
            OtherDeductions = otherDeductions,
            TotalDeductions = totalDeductions,
            NetSalary = net,
            PaymentStatus = PayslipPaymentStatus.Pending,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkPaid(string? paymentReference = null)
    {
        PaymentStatus = PayslipPaymentStatus.Paid;
        DisbursedAt = DateTimeOffset.UtcNow;
        PaymentReference = paymentReference?.Trim();
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}
