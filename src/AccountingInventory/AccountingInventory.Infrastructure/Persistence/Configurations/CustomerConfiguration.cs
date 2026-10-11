using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Name).IsRequired().HasMaxLength(200);
        builder.Property(customer => customer.Mobile).IsRequired().HasMaxLength(20);
        builder.Property(customer => customer.Address).IsRequired().HasMaxLength(500);
        builder.Property(customer => customer.Email).HasMaxLength(256);
        builder.HasIndex(customer => customer.Mobile).IsUnique();
        builder.Ignore(customer => customer.DomainEvents);
        builder.HasQueryFilter(customer => !customer.IsDeleted);
    }
}
