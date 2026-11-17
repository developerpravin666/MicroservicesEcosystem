using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class Department : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? HeadOfDepartment { get; private set; }

    public static Department Create(string name, string code, string? description = null, string? headOfDepartment = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Department code is required.", nameof(code));

        return new Department
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Description = description?.Trim(),
            HeadOfDepartment = headOfDepartment?.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, string code, string? description = null, string? headOfDepartment = null, bool isActive = true)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description?.Trim();
        HeadOfDepartment = headOfDepartment?.Trim();
        IsActive = isActive;
        ModifiedAt = DateTimeOffset.UtcNow;
    }
}
