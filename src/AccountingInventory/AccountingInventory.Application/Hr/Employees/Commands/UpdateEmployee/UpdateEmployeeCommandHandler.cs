using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Employees.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Employees.Commands.UpdateEmployee;

public sealed class UpdateEmployeeCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<UpdateEmployeeCommandHandler> logger)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.Id}' was not found.");

        emp.Update(
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

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employee {Id} ({Code}) updated.", emp.Id, emp.EmployeeCode);

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
