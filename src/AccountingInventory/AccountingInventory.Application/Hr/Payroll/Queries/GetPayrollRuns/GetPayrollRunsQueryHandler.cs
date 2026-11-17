using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetPayrollRuns;

public sealed class GetPayrollRunsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPayrollRunsQuery, IReadOnlyList<PayrollRunSummaryDto>>
{
    public async Task<IReadOnlyList<PayrollRunSummaryDto>> Handle(GetPayrollRunsQuery request, CancellationToken cancellationToken)
    {
        var targetYear = request.Year.GetValueOrDefault(DateTime.UtcNow.Year);

        return await db.PayrollRuns
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Year == targetYear)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Select(p => new PayrollRunSummaryDto(
                p.Id, p.Month, p.Year, p.RunDate, p.TotalEmployees,
                p.TotalGrossAmount, p.TotalDeductions, p.TotalNetAmount,
                p.Status, p.PaymentDate, p.ProcessedBy, p.Notes, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
