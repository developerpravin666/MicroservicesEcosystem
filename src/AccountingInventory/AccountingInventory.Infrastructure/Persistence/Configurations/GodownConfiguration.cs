using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class GodownConfiguration : IEntityTypeConfiguration<Godown>
{
    public void Configure(EntityTypeBuilder<Godown> builder)
    {
        builder.ToTable("godowns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.IsPrimary).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

