using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class CustomerBillSettingsConfiguration : IEntityTypeConfiguration<CustomerBillSettings>
{
    public void Configure(EntityTypeBuilder<CustomerBillSettings> builder)
    {
        builder.ToTable("customer_bill_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(settings => settings.CompanyAddress).IsRequired().HasMaxLength(500);
        builder.Property(settings => settings.CompanyMobile).IsRequired().HasMaxLength(20);
        builder.Property(settings => settings.CompanyEmail).HasMaxLength(256);
        builder.Property(settings => settings.TaxRegistrationNumber).HasMaxLength(50);
        builder.Property(settings => settings.BillTitle).IsRequired().HasMaxLength(80);
        builder.Property(settings => settings.FooterNote).IsRequired().HasMaxLength(500);
        builder.Property(settings => settings.PaperSize).IsRequired().HasMaxLength(20);
        builder.Ignore(settings => settings.DomainEvents);
        builder.HasQueryFilter(settings => !settings.IsDeleted);
    }
}
