using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record CreateEmployeeRequest(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Phone,
    string Designation,
    DateOnly? DateOfJoining,
    string? JoiningDate,
    string? Email,
    string? Department,
    string? DepartmentId,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentType>))]
    EmploymentType EmploymentType,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentStatus>))]
    EmploymentStatus EmploymentStatus,
    string? Status,
    decimal? MonthlyBaseSalary,
    decimal? BaseSalary,
    string? SalaryType, // "Monthly" or "YearlyPackage"
    Guid? ReportsToEmployeeId,
    string? ReportsToEmployeeName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? PanNumber,
    string? Pan,
    string? AadhaarNumber,
    string? Aadhaar,
    string? UanNumber,
    string? Uan,
    string? BankAccountNumber,
    string? BankName,
    string? BankIfscCode,
    string? BankIfsc,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Notes);
