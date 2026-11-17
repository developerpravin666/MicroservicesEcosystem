using System.Text.Json.Serialization;
using AccountingInventory.Application.Hr.Common;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Hr.Leaves.Models;

public sealed record LeaveRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason,
    [property: JsonConverter(typeof(FlexibleEnumConverter<LeaveRequestStatus>))]
    LeaveRequestStatus Status,
    string? ApproverComment,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt);
