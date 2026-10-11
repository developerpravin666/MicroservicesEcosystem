using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccountingInventory.Infrastructure.Persistence.Configurations;

public sealed class AccountingUserPermissionConfiguration : IEntityTypeConfiguration<AccountingUserPermission>
{
    public void Configure(EntityTypeBuilder<AccountingUserPermission> builder)
    {
        builder.ToTable("accounting_user_permissions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.PermissionCode).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.PermissionCode }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class AccountingApprovalConfiguration : IEntityTypeConfiguration<AccountingApproval>
{
    public void Configure(EntityTypeBuilder<AccountingApproval> builder)
    {
        builder.ToTable("accounting_approvals"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ResourceId).HasMaxLength(128).IsRequired(); builder.Property(x => x.Summary).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired(); builder.Property(x => x.DecisionNote).HasMaxLength(500);
        builder.Property(x => x.Revision).HasDefaultValue(0L).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Status, x.CreatedAt }); builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class AccountingDimensionConfiguration : IEntityTypeConfiguration<AccountingDimension>
{
    public void Configure(EntityTypeBuilder<AccountingDimension> builder)
    {
        builder.ToTable("accounting_dimensions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired(); builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DimensionType).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.DimensionType, x.Code }).IsUnique(); builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class SupportingDocumentConfiguration : IEntityTypeConfiguration<SupportingDocument>
{
    public void Configure(EntityTypeBuilder<SupportingDocument> builder)
    {
        builder.ToTable("supporting_documents"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ResourceType).HasMaxLength(50).IsRequired(); builder.Property(x => x.ResourceId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired(); builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StorageReference).HasMaxLength(1000).IsRequired(); builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId }); builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> builder)
    {
        builder.ToTable("fixed_assets"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.AssetNumber).IsUnique();
        builder.Property(x => x.AssetNumber).HasMaxLength(40).IsRequired(); builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AcquisitionDate).HasColumnType("date"); builder.Property(x => x.DisposedDate).HasColumnType("date");
        builder.Property(x => x.AcquisitionCost).HasPrecision(18, 2); builder.Property(x => x.SalvageValue).HasPrecision(18, 2);
        builder.Property(x => x.AccumulatedDepreciation).HasPrecision(18, 2);
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(x => x.AssetAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(x => x.DepreciationExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(x => x.AccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.BookValue); builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class AccountBudgetConfiguration : IEntityTypeConfiguration<AccountBudget>
{
    public void Configure(EntityTypeBuilder<AccountBudget> builder)
    {
        builder.ToTable("account_budgets"); builder.HasKey(x => x.Id);
        builder.Property(x => x.StartDate).HasColumnType("date"); builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.Amount).HasPrecision(18, 2); builder.Property(x => x.Notes).HasMaxLength(300);
        builder.HasIndex(x => new { x.AccountId, x.DimensionId, x.StartDate, x.EndDate }).IsUnique();
        builder.HasOne<ChartAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingDimension>().WithMany().HasForeignKey(x => x.DimensionId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(x => x.DomainEvents); builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
