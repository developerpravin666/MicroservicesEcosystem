using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Payroll.Models;

public sealed record PayslipDto(
    Guid Id,
    Guid PayrollRunId,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string? Department,
    string Designation,
    int Month,
    int Year,
    int WorkingDays,
    int PresentDays,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    decimal BasicSalary,
    decimal Hra,
    decimal Allowances,
    decimal Bonus,
    decimal GrossEarnings,
    decimal PfDeduction,
    decimal EsiDeduction,
    decimal ProfessionalTax,
    decimal TdsDeduction,
    decimal LopDeduction,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetSalary,
    [property: JsonConverter(typeof(FlexibleEnumConverter<PayslipPaymentStatus>))]
    PayslipPaymentStatus PaymentStatus,
    DateTimeOffset? DisbursedAt,
    string? PaymentReference);
