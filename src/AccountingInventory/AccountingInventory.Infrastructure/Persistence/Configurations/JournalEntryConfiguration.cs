using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");
        builder.HasKey(entry => entry.Id);
        builder.HasIndex(entry => entry.JournalNumber).IsUnique();
        builder.HasIndex(entry => entry.JournalDate);
        builder.HasIndex(entry => entry.ReversalOfJournalEntryId).IsUnique()
            .HasFilter("\"reversal_of_journal_entry_id\" IS NOT NULL");
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(entry => entry.ReversalOfJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entry => new { entry.SourceType, entry.SourceId }).IsUnique()
            .HasFilter("\"source_type\" IS NOT NULL AND \"source_id\" IS NOT NULL");
        builder.Property(entry => entry.JournalNumber).HasMaxLength(50).IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(500).IsRequired();
        builder.Property(entry => entry.SourceType).HasMaxLength(60);
        builder.Property(entry => entry.SourceId).HasMaxLength(128);
        builder.Ignore(entry => entry.DomainEvents);
        builder.Ignore(entry => entry.Lines);
        builder.HasQueryFilter(entry => !entry.IsDeleted);
    }
}
