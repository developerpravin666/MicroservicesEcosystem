using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261008120000_AddAuditLogs")]
public sealed class AddAuditLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                table_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                old_value = table.Column<string>(type: "text", nullable: true),
                new_value = table.Column<string>(type: "text", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                tenant_schema = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_audit_logs", x => x.id));

        migrationBuilder.CreateIndex("ix_audit_logs_created_date", "audit_logs", "created_date");
        migrationBuilder.CreateIndex("ix_audit_logs_table_name_created_date", "audit_logs", new[] { "table_name", "created_date" });
        migrationBuilder.CreateIndex("ix_audit_logs_table_name_record_id_created_date", "audit_logs", new[] { "table_name", "record_id", "created_date" });
        migrationBuilder.CreateIndex("ix_audit_logs_created_by_created_date", "audit_logs", new[] { "created_by", "created_date" });
        migrationBuilder.CreateIndex("ix_audit_logs_action", "audit_logs", "action");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "audit_logs");
}
