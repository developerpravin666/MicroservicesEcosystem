using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Employees.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<CreateEmployeeCommandHandler> logger)
    : IRequestHandler<CreateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var code = request.EmployeeCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            var now = DateTime.UtcNow;
            var prefix = $"{now.Year:D4}-{now.Month:D2}-";
            var existingCodes = await db.Employees
                .AsNoTracking()
                .Where(e => e.EmployeeCode.StartsWith(prefix))
                .Select(e => e.EmployeeCode)
                .ToListAsync(cancellationToken);

            var maxSeq = 0;
            foreach (var c in existingCodes)
            {
                var suffix = c.Substring(prefix.Length);
                if (int.TryParse(suffix, out var seq) && seq > maxSeq)
                {
                    maxSeq = seq;
                }
            }

            code = $"{prefix}{(maxSeq + 1):D6}";
        }

        var exists = await db.Employees.AnyAsync(e => e.EmployeeCode == code && !e.IsDeleted, cancellationToken);
        if (exists)
        {
            logger.LogWarning("Employee with code '{Code}' already exists.", code);
            throw new ConflictException($"Employee with code '{code}' already exists.");
        }

        var emp = Employee.Create(
            code,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.Designation,
            request.DateOfJoining,
            request.Email,
            request.Department,
            request.EmploymentType,
            request.EmploymentStatus,
            request.MonthlyBaseSalary,
            request.SalaryType,
            request.ReportsToEmployeeId,
            request.ReportsToEmployeeName,
            request.DateOfBirth,
            request.Gender,
            request.PanNumber,
            request.AadhaarNumber,
            request.UanNumber,
            request.BankAccountNumber,
            request.BankName,
            request.BankIfscCode,
            request.Address,
            request.EmergencyContactName,
            request.EmergencyContactPhone,
            request.Notes);

        db.Employees.Add(emp);

        // Auto-allocate leave balances for the current calendar year from standard leave types
        var currentYear = DateTime.UtcNow.Year;
        var standardLeaveTypes = await db.LeaveTypes
            .Where(lt => !lt.IsDeleted && lt.IsActive)
            .ToListAsync(cancellationToken);

        var employeeFullName = $"{emp.FirstName} {emp.LastName}".Trim();
        foreach (var lt in standardLeaveTypes)
        {
            var balance = LeaveBalance.Create(
                emp.Id,
                emp.EmployeeCode,
                employeeFullName,
                lt.Id,
                lt.Name,
                currentYear,
                lt.DaysAllowedPerYear);
            db.LeaveBalances.Add(balance);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Employee {Code} ({FullName}) registered with Id {Id}.", emp.EmployeeCode, employeeFullName, emp.Id);

        return new EmployeeDetailDto(
            emp.Id, emp.EmployeeCode, emp.FirstName, emp.LastName, emp.Email, emp.Phone,
            emp.Department ?? "General", emp.Designation, emp.DateOfJoining, emp.DateOfBirth,
            emp.Gender, emp.EmploymentType, emp.EmploymentStatus, emp.PanNumber,
            emp.AadhaarNumber, emp.UanNumber, emp.BankAccountNumber, emp.BankName,
            emp.BankIfscCode, emp.MonthlyBaseSalary, emp.SalaryType, emp.ReportsToEmployeeId,
            emp.ReportsToEmployeeName, emp.Address, emp.EmergencyContactName,
            emp.EmergencyContactPhone, emp.Notes, emp.IsActive, emp.CreatedAt);
    }
}
