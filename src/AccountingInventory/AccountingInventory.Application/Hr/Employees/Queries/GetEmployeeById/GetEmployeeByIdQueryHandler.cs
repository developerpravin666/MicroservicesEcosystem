using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Employees.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Employees.Queries.GetEmployeeById;

public sealed class GetEmployeeByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.Id}' was not found.");

        return new EmployeeDetailDto(
            emp.Id, emp.EmployeeCode, emp.FirstName, emp.LastName, emp.Email, emp.Phone,
            emp.Department ?? "General", emp.Designation, emp.DateOfJoining, emp.DateOfBirth,
            emp.Gender, emp.EmploymentType, emp.EmploymentStatus, emp.PanNumber,
            emp.AadhaarNumber, emp.UanNumber, emp.BankAccountNumber, emp.BankName,
            emp.BankIfscCode, emp.MonthlyBaseSalary, emp.SalaryType ?? "Monthly",
            emp.ReportsToEmployeeId, emp.ReportsToEmployeeName, emp.Address,
            emp.EmergencyContactName, emp.EmergencyContactPhone, emp.Notes, emp.IsActive, emp.CreatedAt);
    }
}
