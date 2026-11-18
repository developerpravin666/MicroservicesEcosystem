using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DeleteSalaryStructure;

public sealed class DeleteSalaryStructureCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DeleteSalaryStructureCommandHandler> logger)
    : IRequestHandler<DeleteSalaryStructureCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSalaryStructureCommand request, CancellationToken cancellationToken)
    {
        var structure = await db.SalaryStructures.FirstOrDefaultAsync(s => s.EmployeeId == request.EmployeeId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Salary structure for employee '{request.EmployeeId}' was not found.");

        structure.Delete();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Salary structure for employee {EmployeeId} deleted.", request.EmployeeId);
        return Unit.Value;
    }
}
