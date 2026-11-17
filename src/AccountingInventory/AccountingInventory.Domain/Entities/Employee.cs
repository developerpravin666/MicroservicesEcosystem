using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum EmploymentType
{
    FullTime = 1,
    PartTime = 2,
    Contract = 3,
    Intern = 4
}

public enum EmploymentStatus
{
    Active = 1,
    OnProbation = 2,
    OnLeave = 3,
    Resigned = 4,
    Terminated = 5
}

/// <summary>
/// Employee profile entity in the Smart HR module.
/// </summary>
public sealed class Employee : AggregateRoot
{
    public string EmployeeCode { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? Email { get; private set; }
    public string Phone { get; private set; } = null!;
    public string? Department { get; private set; }
    public string Designation { get; private set; } = null!;
    public DateOnly DateOfJoining { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public EmploymentType EmploymentType { get; private set; } = EmploymentType.FullTime;
    public EmploymentStatus EmploymentStatus { get; private set; } = EmploymentStatus.Active;
    
    // Statutory & Compliance (PAN, Aadhaar, UAN/PF)
    public string? PanNumber { get; private set; }
    public string? AadhaarNumber { get; private set; }
    public string? UanNumber { get; private set; }
    
    // Banking Details for Payroll Disbursement
    public string? BankAccountNumber { get; private set; }
    public string? BankName { get; private set; }
    public string? BankIfscCode { get; private set; }
    public decimal MonthlyBaseSalary { get; private set; } = 0m;
    public string SalaryType { get; private set; } = "Monthly"; // "Monthly" or "YearlyPackage" (CTC)
    public Guid? ReportsToEmployeeId { get; private set; }
    public string? ReportsToEmployeeName { get; private set; }
    
    // Address & Emergency Contacts
    public string? Address { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? Notes { get; private set; }

    public static Employee Create(
        string employeeCode,
        string firstName,
        string lastName,
        string phone,
        string designation,
        DateOnly dateOfJoining,
        string? email = null,
        string? department = null,
        EmploymentType employmentType = EmploymentType.FullTime,
        EmploymentStatus employmentStatus = EmploymentStatus.Active,
        decimal monthlyBaseSalary = 0m,
        string salaryType = "Monthly",
        Guid? reportsToEmployeeId = null,
        string? reportsToEmployeeName = null,
        DateOnly? dateOfBirth = null,
        string? gender = null,
        string? panNumber = null,
        string? aadhaarNumber = null,
        string? uanNumber = null,
        string? bankAccountNumber = null,
        string? bankName = null,
        string? bankIfscCode = null,
        string? address = null,
        string? emergencyContactName = null,
        string? emergencyContactPhone = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new ArgumentException("Employee code is required.", nameof(employeeCode));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));
        if (string.IsNullOrWhiteSpace(designation))
            throw new ArgumentException("Designation is required.", nameof(designation));

        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Phone = phone.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            Department = string.IsNullOrWhiteSpace(department) ? "General" : department.Trim(),
            Designation = designation.Trim(),
            DateOfJoining = dateOfJoining,
            EmploymentType = employmentType,
            EmploymentStatus = employmentStatus,
            MonthlyBaseSalary = monthlyBaseSalary < 0m ? 0m : monthlyBaseSalary,
            SalaryType = string.IsNullOrWhiteSpace(salaryType) ? "Monthly" : salaryType.Trim(),
            ReportsToEmployeeId = reportsToEmployeeId,
            ReportsToEmployeeName = reportsToEmployeeName?.Trim(),
            DateOfBirth = dateOfBirth,
            Gender = gender?.Trim(),
            PanNumber = panNumber?.Trim()?.ToUpperInvariant(),
            AadhaarNumber = aadhaarNumber?.Trim(),
            UanNumber = uanNumber?.Trim(),
            BankAccountNumber = bankAccountNumber?.Trim(),
            BankName = bankName?.Trim(),
            BankIfscCode = bankIfscCode?.Trim()?.ToUpperInvariant(),
            Address = address?.Trim(),
            EmergencyContactName = emergencyContactName?.Trim(),
            EmergencyContactPhone = emergencyContactPhone?.Trim(),
            Notes = notes?.Trim(),
            IsActive = employmentStatus == EmploymentStatus.Active || employmentStatus == EmploymentStatus.OnProbation,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(
        string firstName,
        string lastName,
        string phone,
        string designation,
        DateOnly dateOfJoining,
        string? email = null,
        string? department = null,
        EmploymentType employmentType = EmploymentType.FullTime,
        EmploymentStatus employmentStatus = EmploymentStatus.Active,
        decimal monthlyBaseSalary = 0m,
        string salaryType = "Monthly",
        Guid? reportsToEmployeeId = null,
        string? reportsToEmployeeName = null,
        DateOnly? dateOfBirth = null,
        string? gender = null,
        string? panNumber = null,
        string? aadhaarNumber = null,
        string? uanNumber = null,
        string? bankAccountNumber = null,
        string? bankName = null,
        string? bankIfscCode = null,
        string? address = null,
        string? emergencyContactName = null,
        string? emergencyContactPhone = null,
        string? notes = null)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Department = string.IsNullOrWhiteSpace(department) ? "General" : department.Trim();
        Designation = designation.Trim();
        DateOfJoining = dateOfJoining;
        EmploymentType = employmentType;
        EmploymentStatus = employmentStatus;
        MonthlyBaseSalary = monthlyBaseSalary < 0m ? 0m : monthlyBaseSalary;
        SalaryType = string.IsNullOrWhiteSpace(salaryType) ? "Monthly" : salaryType.Trim();
        ReportsToEmployeeId = reportsToEmployeeId;
        ReportsToEmployeeName = reportsToEmployeeName?.Trim();
        DateOfBirth = dateOfBirth;
        Gender = gender?.Trim();
        PanNumber = panNumber?.Trim()?.ToUpperInvariant();
        AadhaarNumber = aadhaarNumber?.Trim();
        UanNumber = uanNumber?.Trim();
        BankAccountNumber = bankAccountNumber?.Trim();
        BankName = bankName?.Trim();
        BankIfscCode = bankIfscCode?.Trim()?.ToUpperInvariant();
        Address = address?.Trim();
        EmergencyContactName = emergencyContactName?.Trim();
        EmergencyContactPhone = emergencyContactPhone?.Trim();
        Notes = notes?.Trim();
        IsActive = employmentStatus == EmploymentStatus.Active || employmentStatus == EmploymentStatus.OnProbation;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(EmploymentStatus status)
    {
        EmploymentStatus = status;
        IsActive = status == EmploymentStatus.Active || status == EmploymentStatus.OnProbation;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        EmploymentStatus = EmploymentStatus.Terminated;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}
