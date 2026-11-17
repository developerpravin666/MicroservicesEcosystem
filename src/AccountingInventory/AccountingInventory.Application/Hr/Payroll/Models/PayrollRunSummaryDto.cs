using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Payroll.Models;

public sealed record PayrollRunSummaryDto(
    Guid Id,
    int Month,
    int Year,
    DateOnly RunDate,
    int TotalEmployees,
    decimal TotalGrossAmount,
    decimal TotalDeductions,
    decimal TotalNetAmount,
    [property: JsonConverter(typeof(FlexibleEnumConverter<PayrollRunStatus>))]
    PayrollRunStatus Status,
    DateOnly? PaymentDate,
    string? ProcessedBy,
    string? Notes,
    DateTimeOffset CreatedAt);
