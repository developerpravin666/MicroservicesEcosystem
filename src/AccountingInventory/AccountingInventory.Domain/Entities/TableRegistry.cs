namespace AccountingInventory.Domain.Entities;

public sealed class TableRegistry
{
    public Guid Id { get; set; }
    public string SchemaName { get; set; } = "tenant";
    public string TableName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TableType { get; set; } = "Data"; // "Master" or "Data"
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
}

