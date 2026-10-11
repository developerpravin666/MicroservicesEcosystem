namespace AccountingInventory.Domain.Common;

/// <summary>
/// Base type for every aggregate/entity that must be automatically audited by the
/// infrastructure-layer SaveChanges interceptor. Pure domain concept — no EF Core
/// or any infrastructure dependency leaks in here.
/// </summary>
public abstract class AuditableEntity
{
    public required Guid Id { get; init; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}
