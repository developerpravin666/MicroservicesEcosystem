namespace AccountingInventory.Domain.Entities;

/// <summary>Immutable record of a committed change to a tenant data entity.</summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string TableName { get; private set; } = null!;
    public string RecordId { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedDate { get; private set; }
    public string TenantSchema { get; private set; } = null!;

    public static AuditLog Create(string tableName, string recordId, string action,
        string? oldValue, string? newValue, Guid? createdBy, DateTimeOffset createdDate, string tenantSchema)
        => new()
        {
            Id = Guid.NewGuid(),
            TableName = tableName,
            RecordId = recordId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedBy = createdBy,
            CreatedDate = createdDate,
            TenantSchema = tenantSchema
        };

    private AuditLog() { }
}
