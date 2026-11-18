using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// Master dictionary entry for HR & Payroll modules (e.g. Job Titles/Designations, 
/// Employment Types, Employee Statuses, Salary Types, Leave Categories, Godowns, etc.)
/// </summary>
public sealed class HrMasterItem : AggregateRoot
{
    public string Category { get; private set; } = null!; // e.g. "JOB_TITLE", "EMPLOYMENT_TYPE", "EMPLOYEE_STATUS", "SALARY_TYPE", "LEAVE_CATEGORY"
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; } = false;

    public static HrMasterItem Create(
        string category,
        string name,
        string code,
        string? description = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Master category is required.", nameof(category));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Master name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Master code is required.", nameof(code));

        return new HrMasterItem
        {
            Id = Guid.NewGuid(),
            Category = category.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            IsDefault = isDefault,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, string? description, bool isDefault, bool isActive = true)
    {
        Name = name.Trim();
        Description = description?.Trim();
        IsDefault = isDefault;
        IsActive = isActive;
        ModifiedAt = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        IsActive = false;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}
