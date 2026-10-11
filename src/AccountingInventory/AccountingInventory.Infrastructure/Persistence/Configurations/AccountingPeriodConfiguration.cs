using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("accounting_periods");
        builder.HasKey(period => period.Id);
        builder.Property(period => period.Name).IsRequired().HasMaxLength(100);
        builder.Property(period => period.StartDate).HasColumnType("date");
        builder.Property(period => period.EndDate).HasColumnType("date");
        builder.HasIndex(period => period.Name).IsUnique();
        builder.HasIndex(period => new { period.StartDate, period.EndDate });
        builder.Ignore(period => period.DomainEvents);
        builder.HasQueryFilter(period => !period.IsDeleted);
    }
}
