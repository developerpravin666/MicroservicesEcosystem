using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class ChartAccountConfiguration : IEntityTypeConfiguration<ChartAccount>
{
    public void Configure(EntityTypeBuilder<ChartAccount> builder)
    {
        builder.ToTable("chart_accounts");
        builder.HasKey(account => account.Id);
        builder.HasIndex(account => account.Code).IsUnique();
        builder.Property(account => account.Code).HasMaxLength(20).IsRequired();
        builder.Property(account => account.Name).HasMaxLength(150).IsRequired();
        builder.Property(account => account.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(account => account.NormalBalance).HasConversion<string>().HasMaxLength(10);
        builder.Ignore(account => account.DomainEvents);
        builder.HasQueryFilter(account => !account.IsDeleted);
    }
}
