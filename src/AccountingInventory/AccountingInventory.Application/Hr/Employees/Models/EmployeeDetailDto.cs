using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Employees.Models;

public sealed record EmployeeDetailDto(
    Guid Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string? Email,
    string Phone,
    string Department,
    string Designation,
    DateOnly DateOfJoining,
    DateOnly? DateOfBirth,
    string? Gender,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentType>))]
    EmploymentType EmploymentType,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentStatus>))]
    EmploymentStatus EmploymentStatus,
    string? PanNumber,
    string? AadhaarNumber,
    string? UanNumber,
    string? BankAccountNumber,
    string? BankName,
    string? BankIfscCode,
    decimal MonthlyBaseSalary,
    string SalaryType,
    Guid? ReportsToEmployeeId,
    string? ReportsToEmployeeName,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);
