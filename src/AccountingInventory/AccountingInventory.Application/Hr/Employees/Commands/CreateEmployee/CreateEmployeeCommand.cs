using AccountingInventory.Application.Hr.Employees.Models;
using AccountingInventory.Domain.Entities;
using MediatR;

namespace AccountingInventory.Application.Hr.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Phone,
    string Designation,
    DateOnly DateOfJoining,
    string? Email = null,
    string? Department = null,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    EmploymentStatus EmploymentStatus = EmploymentStatus.Active,
    decimal MonthlyBaseSalary = 0m,
    string SalaryType = "Monthly",
    Guid? ReportsToEmployeeId = null,
    string? ReportsToEmployeeName = null,
    DateOnly? DateOfBirth = null,
    string? Gender = null,
    string? PanNumber = null,
    string? AadhaarNumber = null,
    string? UanNumber = null,
    string? BankAccountNumber = null,
    string? BankName = null,
    string? BankIfscCode = null,
    string? Address = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? Notes = null) : IRequest<EmployeeDetailDto>;
