using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("bank_reconciliations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StatementReference).IsRequired().HasMaxLength(100);
        builder.Property(item => item.StartDate).HasColumnType("date");
        builder.Property(item => item.EndDate).HasColumnType("date");
        builder.Property(item => item.OpeningBalance).HasPrecision(18, 2);
        builder.Property(item => item.ClosingBalance).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.AccountId, item.StatementReference }).IsUnique();
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(item => item.DomainEvents);
        builder.Ignore(item => item.Lines);
        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}

public sealed class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ToTable("bank_statement_lines");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Description).IsRequired().HasMaxLength(300);
        builder.Property(item => item.Reference).HasMaxLength(100);
        builder.Property(item => item.TransactionDate).HasColumnType("date");
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.HasIndex(item => item.BankReconciliationId);
        builder.HasIndex(item => item.JournalLineId).IsUnique().HasFilter("\"journal_line_id\" IS NOT NULL");
        builder.HasOne<BankReconciliation>().WithMany().HasForeignKey(item => item.BankReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalLine>().WithMany().HasForeignKey(item => item.JournalLineId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(item => item.DomainEvents);
        builder.HasQueryFilter(item => !item.IsDeleted);
    }
}
