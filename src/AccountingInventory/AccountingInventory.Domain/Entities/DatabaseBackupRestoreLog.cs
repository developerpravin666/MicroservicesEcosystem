namespace AccountingInventory.Domain.Entities;

public sealed class DatabaseBackupRestoreLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? BackupId { get; set; }
    public string BackupSchemaName { get; set; } = string.Empty;
    public string TargetSchema { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // BACKUP_CREATED, RESTORE_EXECUTED, SCHEMA_DROPPED
    public string Status { get; set; } = "Success"; // Success, Failed, Partial
    public long TotalRowsProcessed { get; set; }
    public int TotalInserted { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalErrors { get; set; }
    public string? DetailsJson { get; set; }
    public string? Message { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public string ExecutedBy { get; set; } = string.Empty;
}

