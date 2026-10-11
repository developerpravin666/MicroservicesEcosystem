using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class FinanceVendorConfiguration : IEntityTypeConfiguration<FinanceVendor>
{
    public void Configure(EntityTypeBuilder<FinanceVendor> builder)
    {
        builder.ToTable(Constants.DBConstants.DBTableNames.FinanceVendors);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Mobile).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.ContactName).HasMaxLength(100);
        builder.Property(x => x.ContactMobile).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.Code);
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.Mobile);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
