using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.TableName).IsRequired().HasMaxLength(128);
        builder.Property(log => log.RecordId).IsRequired().HasMaxLength(128);
        builder.Property(log => log.Action).IsRequired().HasMaxLength(20);
        builder.Property(log => log.OldValue).HasColumnType("text");
        builder.Property(log => log.NewValue).HasColumnType("text");
        builder.Property(log => log.TenantSchema).IsRequired().HasMaxLength(128);
        builder.HasIndex(log => log.CreatedDate);
        builder.HasIndex(log => new { log.TableName, log.CreatedDate });
        builder.HasIndex(log => new { log.TableName, log.RecordId, log.CreatedDate });
        builder.HasIndex(log => new { log.CreatedBy, log.CreatedDate });
        builder.HasIndex(log => log.Action);
    }
}
