using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DeletePayrollRun;

public sealed class DeletePayrollRunCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DeletePayrollRunCommandHandler> logger)
    : IRequestHandler<DeletePayrollRunCommand, Unit>
{
    public async Task<Unit> Handle(DeletePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, cancellationToken)
            ?? throw new NotFoundException($"Payroll run with ID '{request.Id}' was not found.");

        run.Delete();

        var payslips = await db.Payslips
            .Where(p => p.PayrollRunId == request.Id && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var slip in payslips)
        {
            slip.Delete();
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Payroll run {Id} ({Month}/{Year}) and {Count} payslips marked as deleted.", run.Id, run.Month, run.Year, payslips.Count);

        return Unit.Value;
    }
}
