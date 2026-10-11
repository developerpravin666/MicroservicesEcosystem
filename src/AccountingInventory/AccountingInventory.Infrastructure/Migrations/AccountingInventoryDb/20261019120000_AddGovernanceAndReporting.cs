using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261019120000_AddGovernanceAndReporting")]
public sealed class AddGovernanceAndReporting : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Some tenant schemas can have the General Ledger migration recorded in
        // history even though its tables are missing (for example, after an
        // earlier update targeted a different schema). Repair missing tables
        // without replacing or clearing any existing ledger data before altering
        // journal_lines below.
        m.Sql(JournalLedgerTableRepair.Sql);
        m.Sql("""
            ALTER TABLE journal_lines ADD COLUMN IF NOT EXISTS dimension_id uuid NULL;
            CREATE INDEX IF NOT EXISTS ix_journal_lines_dimension_id ON journal_lines (dimension_id);
            """);
        m.CreateTable("accounting_approvals", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"),
            Action = t.Column<int>(type: "integer", nullable: false, name: "action"),
            ResourceId = t.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, name: "resource_id"),
            Summary = t.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, name: "summary"),
            PayloadJson = t.Column<string>(type: "jsonb", nullable: false, name: "payload_json"),
            RequestedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "requested_by"),
            DecidedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "decided_by"),
            Status = t.Column<int>(type: "integer", nullable: false, name: "status"),
            DecisionNote = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, name: "decision_note"),
            DecidedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "decided_at"),
            AppliedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "applied_at"),
            Revision = t.Column<long>(type: "bigint", nullable: false, defaultValue: 0L, name: "revision"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"),
            CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"),
            ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t => t.PrimaryKey("pk_accounting_approvals", x => x.Id));
        m.CreateTable("accounting_user_permissions", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"),
            UserId = t.Column<Guid>(type: "uuid", nullable: false, name: "user_id"),
            PermissionCode = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, name: "permission_code"),
            GrantedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "granted_by"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"),
            CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"),
            ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_accounting_user_permissions", x => x.Id);
            t.ForeignKey("fk_accounting_user_permissions_users_user_id", x => x.UserId, "users", "id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateTable("accounting_dimensions", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"),
            Code = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, name: "code"),
            Name = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, name: "name"),
            DimensionType = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, name: "dimension_type"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"),
            CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"),
            ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t => t.PrimaryKey("pk_accounting_dimensions", x => x.Id));
        m.CreateTable("supporting_documents", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"),
            ResourceType = t.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, name: "resource_type"),
            ResourceId = t.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, name: "resource_id"),
            FileName = t.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, name: "file_name"),
            ContentType = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, name: "content_type"),
            StorageReference = t.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, name: "storage_reference"),
            Description = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, name: "description"),
            AddedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "added_by"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"),
            CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"),
            ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t => t.PrimaryKey("pk_supporting_documents", x => x.Id));
        m.CreateTable("fixed_assets", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"),
            AssetNumber = t.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, name: "asset_number"),
            Name = t.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, name: "name"),
            AcquisitionDate = t.Column<DateOnly>(type: "date", nullable: false, name: "acquisition_date"),
            AcquisitionCost = t.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, name: "acquisition_cost"),
            SalvageValue = t.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, name: "salvage_value"),
            UsefulLifeMonths = t.Column<int>(type: "integer", nullable: false, name: "useful_life_months"),
            AccumulatedDepreciation = t.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, name: "accumulated_depreciation"),
            AssetAccountId = t.Column<Guid>(type: "uuid", nullable: false, name: "asset_account_id"),
            DepreciationExpenseAccountId = t.Column<Guid>(type: "uuid", nullable: false, name: "depreciation_expense_account_id"),
            AccumulatedDepreciationAccountId = t.Column<Guid>(type: "uuid", nullable: false, name: "accumulated_depreciation_account_id"),
            DisposedDate = t.Column<DateOnly>(type: "date", nullable: true, name: "disposed_date"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"),
            CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"),
            ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"),
            IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_fixed_assets", x => x.Id);
            t.ForeignKey("fk_fixed_assets_chart_accounts_asset_account_id", x => x.AssetAccountId, "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("fk_fixed_assets_chart_accounts_depreciation_expense_account_id", x => x.DepreciationExpenseAccountId, "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("fk_fixed_assets_chart_accounts_accumulated_depreciation_account_id", x => x.AccumulatedDepreciationAccountId, "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateTable("account_budgets", t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false, name: "id"), AccountId = t.Column<Guid>(type: "uuid", nullable: false, name: "account_id"),
            DimensionId = t.Column<Guid>(type: "uuid", nullable: true, name: "dimension_id"), StartDate = t.Column<DateOnly>(type: "date", nullable: false, name: "start_date"),
            EndDate = t.Column<DateOnly>(type: "date", nullable: false, name: "end_date"), Amount = t.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, name: "amount"),
            Notes = t.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, name: "notes"),
            CreatedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, name: "created_at"), CreatedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "created_by"),
            ModifiedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, name: "modified_at"), ModifiedBy = t.Column<Guid>(type: "uuid", nullable: true, name: "modified_by"),
            IsActive = t.Column<bool>(type: "boolean", nullable: false, name: "is_active"), IsDeleted = t.Column<bool>(type: "boolean", nullable: false, name: "is_deleted")
        }, constraints: t =>
        {
            t.PrimaryKey("pk_account_budgets", x => x.Id);
            t.ForeignKey("fk_account_budgets_chart_accounts_account_id", x => x.AccountId, "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("fk_account_budgets_accounting_dimensions_dimension_id", x => x.DimensionId, "accounting_dimensions", "id", onDelete: ReferentialAction.Restrict);
        });
        m.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'fk_journal_lines_accounting_dimensions_dimension_id'
                      AND conrelid = 'journal_lines'::regclass
                ) THEN
                    ALTER TABLE journal_lines
                        ADD CONSTRAINT fk_journal_lines_accounting_dimensions_dimension_id
                        FOREIGN KEY (dimension_id) REFERENCES accounting_dimensions (id) ON DELETE RESTRICT;
                END IF;
            END $$;
            """);
        m.CreateIndex("ix_accounting_approvals_status_created_at", "accounting_approvals", new[] { "status", "created_at" });
        m.CreateIndex("ix_accounting_user_permissions_user_id_permission_code", "accounting_user_permissions", new[] { "user_id", "permission_code" }, unique: true);
        m.CreateIndex("ix_accounting_user_permissions_user_id", "accounting_user_permissions", "user_id");
        m.CreateIndex("ix_accounting_dimensions_dimension_type_code", "accounting_dimensions", new[] { "dimension_type", "code" }, unique: true);
        m.CreateIndex("ix_supporting_documents_resource_type_resource_id", "supporting_documents", new[] { "resource_type", "resource_id" });
        m.CreateIndex("ix_fixed_assets_asset_number", "fixed_assets", "asset_number", unique: true);
        m.CreateIndex("ix_fixed_assets_asset_account_id", "fixed_assets", "asset_account_id");
        m.CreateIndex("ix_fixed_assets_depreciation_expense_account_id", "fixed_assets", "depreciation_expense_account_id");
        m.CreateIndex("ix_fixed_assets_accumulated_depreciation_account_id", "fixed_assets", "accumulated_depreciation_account_id");
        m.CreateIndex("ix_account_budgets_account_id_dimension_id_start_date_end_date", "account_budgets", new[] { "account_id", "dimension_id", "start_date", "end_date" }, unique: true);
        m.CreateIndex("ix_account_budgets_dimension_id", "account_budgets", "dimension_id");
        m.CreateIndex("ix_account_budgets_account_id", "account_budgets", "account_id");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey("fk_journal_lines_accounting_dimensions_dimension_id", "journal_lines");
        m.DropTable("account_budgets"); m.DropTable("fixed_assets"); m.DropTable("supporting_documents");
        m.DropTable("accounting_user_permissions"); m.DropTable("accounting_approvals"); m.DropTable("accounting_dimensions");
        m.DropIndex("ix_journal_lines_dimension_id", "journal_lines"); m.DropColumn("dimension_id", "journal_lines");
    }
}
