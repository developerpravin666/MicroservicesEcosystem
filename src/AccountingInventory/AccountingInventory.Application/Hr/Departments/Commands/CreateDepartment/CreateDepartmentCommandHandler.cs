using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Departments.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Departments.Commands.CreateDepartment;

public sealed class CreateDepartmentCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<CreateDepartmentCommandHandler> logger)
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            throw new BadRequestException("Department Name and Code are required.");

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await db.Departments.AnyAsync(d => d.Code == code && !d.IsDeleted, cancellationToken);
        if (exists)
        {
            logger.LogWarning("Department code '{Code}' already exists.", code);
            throw new ConflictException($"Department code '{code}' already exists.");
        }

        var dept = Department.Create(request.Name, request.Code, request.Description, request.HeadOfDepartment);
        db.Departments.Add(dept);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Department '{Code}' ({Name}) created with Id {Id}.", dept.Code, dept.Name, dept.Id);
        return new DepartmentDto(dept.Id, dept.Name, dept.Code, dept.Description, dept.HeadOfDepartment, dept.IsActive);
    }
}
