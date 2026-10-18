namespace AccountingInventory.Domain.Entities;

public sealed class DatabaseBackupRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BackupName { get; set; } = string.Empty;
    public string BackupSchemaName { get; set; } = string.Empty; // {{actual db name}}_bkp_YYYYMMDD
    public string DatabaseName { get; set; } = string.Empty;
    public string SourceSchema { get; set; } = "tenant";
    public int TotalTables { get; set; }
    public long TotalRows { get; set; }
    public string TablesJson { get; set; } = "[]";
    public string Status { get; set; } = "Active"; // Active, Restored, Dropped
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastRestoredAtUtc { get; set; }
    public string? LastRestoredBy { get; set; }
    public DateTime? DroppedAtUtc { get; set; }
    public string? DroppedBy { get; set; }
}

