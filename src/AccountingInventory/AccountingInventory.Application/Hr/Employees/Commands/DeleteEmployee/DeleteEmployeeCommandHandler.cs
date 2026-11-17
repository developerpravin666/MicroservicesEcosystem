using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Employees.Commands.DeleteEmployee;

public sealed class DeleteEmployeeCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DeleteEmployeeCommandHandler> logger)
    : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.Id && !e.IsDeleted, cancellationToken);
        if (emp is null)
            throw new NotFoundException($"Employee with id '{request.Id}' was not found.");

        emp.Delete();
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employee {Id} ({Code}) soft-deleted.", emp.Id, emp.EmployeeCode);
    }
}
