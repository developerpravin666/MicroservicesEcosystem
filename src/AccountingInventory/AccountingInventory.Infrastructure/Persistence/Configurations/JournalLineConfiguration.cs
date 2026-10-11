using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("journal_lines");
        builder.HasKey(line => line.Id);
        builder.HasIndex(line => line.JournalEntryId);
        builder.HasIndex(line => line.AccountId);
        builder.HasIndex(line => line.DimensionId);
        builder.Property(line => line.Debit).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.Credit).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.Memo).HasMaxLength(250);
        builder.Ignore(line => line.DomainEvents);
        builder.HasQueryFilter(line => !line.IsDeleted);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(line => line.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(line => line.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
