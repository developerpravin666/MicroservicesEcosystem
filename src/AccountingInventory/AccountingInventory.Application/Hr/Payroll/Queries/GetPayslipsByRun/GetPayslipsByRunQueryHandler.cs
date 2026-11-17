using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Hr.Payroll.Queries.GetPayslipsByRun;

public sealed class GetPayslipsByRunQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPayslipsByRunQuery, IReadOnlyList<PayslipDto>>
{
    public async Task<IReadOnlyList<PayslipDto>> Handle(GetPayslipsByRunQuery request, CancellationToken cancellationToken)
    {
        return await db.Payslips
            .AsNoTracking()
            .Where(p => p.PayrollRunId == request.RunId && !p.IsDeleted)
            .OrderBy(p => p.EmployeeName)
            .Select(p => new PayslipDto(
                p.Id, p.PayrollRunId, p.EmployeeId, p.EmployeeCode, p.EmployeeName,
                p.Department, p.Designation, p.Month, p.Year, p.WorkingDays,
                p.PresentDays, p.PaidLeaveDays, p.UnpaidLeaveDays, p.BasicSalary,
                p.Hra, p.Allowances, p.Bonus, p.GrossEarnings, p.PfDeduction,
                p.EsiDeduction, p.ProfessionalTax, p.TdsDeduction, p.LopDeduction,
                p.OtherDeductions, p.TotalDeductions, p.NetSalary, p.PaymentStatus,
                p.DisbursedAt, p.PaymentReference))
            .ToListAsync(cancellationToken);
    }
}
