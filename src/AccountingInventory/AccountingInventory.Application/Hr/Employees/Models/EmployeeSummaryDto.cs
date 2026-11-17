using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Employees.Models;

public sealed record EmployeeSummaryDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string FirstName,
    string LastName,
    string? Email,
    string Phone,
    string Department,
    string Designation,
    DateOnly DateOfJoining,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentType>))]
    EmploymentType EmploymentType,
    [property: JsonConverter(typeof(FlexibleEnumConverter<EmploymentStatus>))]
    EmploymentStatus EmploymentStatus,
    decimal MonthlyBaseSalary,
    string SalaryType,
    Guid? ReportsToEmployeeId,
    string? ReportsToEmployeeName,
    bool IsActive);
