using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.MarkPayslipPaid;

public sealed class MarkPayslipPaidCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<MarkPayslipPaidCommandHandler> logger)
    : IRequestHandler<MarkPayslipPaidCommand, PayslipDto>
{
    public async Task<PayslipDto> Handle(MarkPayslipPaidCommand request, CancellationToken cancellationToken)
    {
        var slip = await db.Payslips.FirstOrDefaultAsync(p => p.Id == request.PayslipId && !p.IsDeleted, cancellationToken);
        if (slip is null)
            throw new NotFoundException($"Payslip with id '{request.PayslipId}' was not found.");

        slip.MarkPaid(request.PaymentReference);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payslip {Id} for employee {Emp} marked paid with ref '{Ref}'.", slip.Id, slip.EmployeeCode, request.PaymentReference);

        return new PayslipDto(
            slip.Id, slip.PayrollRunId, slip.EmployeeId, slip.EmployeeCode, slip.EmployeeName,
            slip.Department, slip.DepartmentName ?? slip.Department, slip.Designation, slip.Month, slip.Year, slip.WorkingDays,
            slip.TotalWorkingDays > 0 ? slip.TotalWorkingDays : slip.WorkingDays,
            slip.PresentDays, slip.PaidLeaveDays, slip.UnpaidLeaveDays, slip.BasicSalary,
            slip.HouseRentAllowance, slip.Hra, slip.Allowances, slip.Bonus, slip.GrossEarnings,
            slip.ProvidentFund > 0m ? slip.ProvidentFund : slip.PfDeduction, slip.PfDeduction,
            slip.EmployeeStateInsurance > 0m ? slip.EmployeeStateInsurance : slip.EsiDeduction, slip.EsiDeduction,
            slip.ProfessionalTax,
            slip.TaxDeductedAtSource > 0m ? slip.TaxDeductedAtSource : slip.TdsDeduction, slip.TdsDeduction,
            slip.LossOfPayDeduction > 0m ? slip.LossOfPayDeduction : slip.LopDeduction, slip.LopDeduction,
            slip.OtherDeductions, slip.TotalDeductions, slip.NetSalary, slip.PaymentStatus,
            slip.DisbursedAt, slip.PaymentReference);
    }
}
