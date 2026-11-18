using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Hr.Payroll.Models;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Hr.Payroll.Commands.ProcessPayrollRun;

public sealed class ProcessPayrollRunCommandHandler(
    IAccountingInventoryDbContext db,
    ILogger<ProcessPayrollRunCommandHandler> logger)
    : IRequestHandler<ProcessPayrollRunCommand, PayrollRunSummaryDto>
{
    public async Task<PayrollRunSummaryDto> Handle(ProcessPayrollRunCommand request, CancellationToken cancellationToken)
    {
        if (request.Month < 1 || request.Month > 12)
            throw new BadRequestException("Month must be between 1 and 12.");

        var existing = await db.PayrollRuns.FirstOrDefaultAsync(
            p => p.Month == request.Month && p.Year == request.Year && p.Status != PayrollRunStatus.Cancelled && !p.IsDeleted,
            cancellationToken);

        if (existing is not null)
            throw new ConflictException($"Payroll for {request.Month}/{request.Year} has already been processed.");

        var activeEmployees = await db.Employees
            .Where(e => !e.IsDeleted && (e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.OnProbation))
            .ToListAsync(cancellationToken);

        if (activeEmployees.Count == 0)
            throw new ConflictException("No active employees found to run payroll.");

        var empIds = activeEmployees.Select(e => e.Id).ToList();
        var structures = await db.SalaryStructures
            .Where(s => empIds.Contains(s.EmployeeId) && !s.IsDeleted)
            .ToDictionaryAsync(s => s.EmployeeId, cancellationToken);

        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var runDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var payslips = new List<Payslip>();
        decimal totalGross = 0m, totalDeductions = 0m, totalNet = 0m;

        foreach (var emp in activeEmployees)
        {
            structures.TryGetValue(emp.Id, out var structure);

            var basic = structure?.BasicSalary ?? (emp.MonthlyBaseSalary > 0 ? emp.MonthlyBaseSalary * 0.5m : 0m);
            var hra = (structure != null && structure.HouseRentAllowance > 0m)
                ? structure.HouseRentAllowance
                : (structure?.Hra ?? (emp.MonthlyBaseSalary > 0 ? emp.MonthlyBaseSalary * 0.25m : 0m));
            var allowances = structure != null
                ? structure.DearnessAllowance + structure.ConveyanceAllowance + structure.SpecialAllowance + structure.MedicalAllowance
                : (emp.MonthlyBaseSalary > 0 ? emp.MonthlyBaseSalary * 0.25m : 0m);
            var bonus = structure?.PerformanceBonus ?? 0m;

            var pf = structure?.ProvidentFund ?? (basic > 0 ? Math.Round(basic * 0.12m, 2) : 0m);
            var esi = structure?.Esi ?? 0m;
            var pt = structure?.ProfessionalTax ?? (basic > 15000 ? 200m : 0m);
            var tds = structure?.TdsTax ?? 0m;
            var otherDed = structure?.OtherDeductions ?? 0m;
            var lop = 0m;

            var payslip = Payslip.Create(
                Guid.Empty,
                emp.Id,
                emp.EmployeeCode,
                $"{emp.FirstName} {emp.LastName}".Trim(),
                emp.Department,
                emp.Designation,
                request.Month,
                request.Year,
                daysInMonth,
                daysInMonth,
                0m,
                0m,
                basic,
                hra,
                allowances,
                bonus,
                pf,
                esi,
                pt,
                tds,
                lop,
                otherDed,
                hra);

            payslips.Add(payslip);
            totalGross += payslip.GrossEarnings;
            totalDeductions += payslip.TotalDeductions;
            totalNet += payslip.NetSalary;
        }

        var payrollRun = PayrollRun.Create(
            request.Month,
            request.Year,
            runDate,
            activeEmployees.Count,
            totalGross,
            totalDeductions,
            totalNet,
            request.ProcessedBy ?? "System Admin",
            request.Notes);

        db.PayrollRuns.Add(payrollRun);

        foreach (var p in payslips)
        {
            typeof(Payslip).GetProperty(nameof(Payslip.PayrollRunId))?.SetValue(p, payrollRun.Id);
            db.Payslips.Add(p);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payroll for {Month}/{Year} processed for {Count} employees. Total Net: {Net}.",
            request.Month, request.Year, activeEmployees.Count, totalNet);

        return new PayrollRunSummaryDto(
            payrollRun.Id, payrollRun.Month, payrollRun.Year, payrollRun.RunDate,
            payrollRun.TotalEmployees, payrollRun.TotalGrossAmount, payrollRun.TotalDeductions,
            payrollRun.TotalNetAmount, payrollRun.Status, payrollRun.PaymentDate,
            payrollRun.ProcessedBy, payrollRun.Notes, payrollRun.CreatedAt);
    }
}
