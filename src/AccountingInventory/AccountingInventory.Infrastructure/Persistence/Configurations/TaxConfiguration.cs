using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class TaxConfiguration : IEntityTypeConfiguration<Tax>
{
    public void Configure(EntityTypeBuilder<Tax> builder)
    {
        builder.ToTable("taxes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Treatment).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.ProfileId, x.EffectiveFrom }).IsUnique().HasFilter("NOT is_deleted");
        builder.Property(x => x.Cgst).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.Sgst).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.TotalTax).HasPrecision(5, 2).IsRequired();
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
