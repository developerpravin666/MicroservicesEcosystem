using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.DisbursePayrollRun;

public sealed class DisbursePayrollRunCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<DisbursePayrollRunCommandHandler> logger)
    : IRequestHandler<DisbursePayrollRunCommand, PayrollRunSummaryDto>
{
    public async Task<PayrollRunSummaryDto> Handle(DisbursePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(p => p.Id == request.RunId && !p.IsDeleted, cancellationToken);
        if (run is null)
            throw new NotFoundException($"Payroll run with id '{request.RunId}' was not found.");

        run.MarkPaid(request.PaymentDate);

        var payslips = await db.Payslips
            .Where(p => p.PayrollRunId == run.Id && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var slip in payslips)
        {
            slip.MarkPaid($"DISB-{run.Year}-{run.Month:D2}");
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payroll run {RunId} disbursed for {Count} payslips on {Date}.", run.Id, payslips.Count, request.PaymentDate);

        return new PayrollRunSummaryDto(
            run.Id, run.Month, run.Year, run.RunDate,
            run.TotalEmployees, run.TotalGrossAmount, run.TotalDeductions,
            run.TotalNetAmount, run.Status, run.PaymentDate,
            run.ProcessedBy, run.Notes, run.CreatedAt);
    }
}
