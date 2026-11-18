using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// Configurable leave category (e.g. Casual Leave, Sick Leave, Earned Leave, Maternity, LOP).
/// </summary>
public sealed class LeaveType : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public int DaysAllowedPerYear { get; private set; } = 12;
    public bool IsPaid { get; private set; } = true;
    public bool IsCarryForward { get; private set; } = false;
    public string? Description { get; private set; }

    public static LeaveType Create(
        string name,
        string code,
        int daysAllowedPerYear = 12,
        bool isPaid = true,
        bool isCarryForward = false,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Leave type name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Leave type code is required.", nameof(code));

        return new LeaveType
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            DaysAllowedPerYear = daysAllowedPerYear,
            IsPaid = isPaid,
            IsCarryForward = isCarryForward,
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(
        string name,
        string code,
        int daysAllowedPerYear,
        bool isPaid,
        bool isCarryForward,
        string? description = null,
        bool isActive = true)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        DaysAllowedPerYear = daysAllowedPerYear;
        IsPaid = isPaid;
        IsCarryForward = isCarryForward;
        Description = description?.Trim();
        IsActive = isActive;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Tracks allocated, consumed, and remaining leave days per employee per year.
/// </summary>
public sealed class LeaveBalance : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public string EmployeeCode { get; private set; } = null!;
    public string EmployeeName { get; private set; } = null!;
    public Guid LeaveTypeId { get; private set; }
    public string LeaveTypeName { get; private set; } = null!;
    public int Year { get; private set; }
    public decimal TotalAllocated { get; private set; }
    public decimal UsedDays { get; private set; } = 0m;
    public decimal PendingApprovalDays { get; private set; } = 0m;
    public decimal AvailableDays => Math.Max(0m, TotalAllocated - UsedDays - PendingApprovalDays);

    public static LeaveBalance Create(
        Guid employeeId,
        string employeeCode,
        string employeeName,
        Guid leaveTypeId,
        string leaveTypeName,
        int year,
        decimal totalAllocated)
    {
        return new LeaveBalance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            EmployeeName = employeeName.Trim(),
            LeaveTypeId = leaveTypeId,
            LeaveTypeName = leaveTypeName.Trim(),
            Year = year,
            TotalAllocated = totalAllocated,
            UsedDays = 0m,
            PendingApprovalDays = 0m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AdjustAllocated(decimal newAllocated)
    {
        TotalAllocated = newAllocated;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void AddPending(decimal days)
    {
        PendingApprovalDays += days;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void RemovePending(decimal days)
    {
        PendingApprovalDays = Math.Max(0m, PendingApprovalDays - days);
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Consume(decimal days)
    {
        PendingApprovalDays = Math.Max(0m, PendingApprovalDays - days);
        UsedDays += days;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Individual leave application submitted by an employee with approval workflow.
/// </summary>
public sealed class LeaveRequest : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public string EmployeeCode { get; private set; } = null!;
    public string EmployeeName { get; private set; } = null!;
    public Guid LeaveTypeId { get; private set; }
    public string LeaveTypeName { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal TotalDays { get; private set; }
    public string Reason { get; private set; } = null!;
    public LeaveRequestStatus Status { get; private set; } = LeaveRequestStatus.Pending;
    public string? ApproverComment { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    public static LeaveRequest Create(
        Guid employeeId,
        string employeeCode,
        string employeeName,
        Guid leaveTypeId,
        string leaveTypeName,
        DateOnly startDate,
        DateOnly endDate,
        decimal totalDays,
        string reason)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be prior to start date.", nameof(endDate));
        if (totalDays <= 0m)
            throw new ArgumentException("Total days must be greater than zero.", nameof(totalDays));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Leave reason is required.", nameof(reason));

        return new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            EmployeeCode = employeeCode.Trim().ToUpperInvariant(),
            EmployeeName = employeeName.Trim(),
            LeaveTypeId = leaveTypeId,
            LeaveTypeName = leaveTypeName.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            TotalDays = totalDays,
            Reason = reason.Trim(),
            Status = LeaveRequestStatus.Pending,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Approve(string approver, string? comment = null)
    {
        Status = LeaveRequestStatus.Approved;
        ApprovedBy = approver.Trim();
        ApproverComment = comment?.Trim();
        ApprovedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(string approver, string? comment = null)
    {
        Status = LeaveRequestStatus.Rejected;
        ApprovedBy = approver.Trim();
        ApproverComment = comment?.Trim();
        ApprovedAt = DateTimeOffset.UtcNow;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = LeaveRequestStatus.Cancelled;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        Guid leaveTypeId,
        string leaveTypeName,
        DateOnly startDate,
        DateOnly endDate,
        decimal totalDays,
        string reason)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be prior to start date.", nameof(endDate));
        if (totalDays <= 0m)
            throw new ArgumentException("Total days must be greater than zero.", nameof(totalDays));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Leave reason is required.", nameof(reason));

        LeaveTypeId = leaveTypeId;
        LeaveTypeName = leaveTypeName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        TotalDays = totalDays;
        Reason = reason.Trim();
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        Status = LeaveRequestStatus.Cancelled;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}
