using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.TenantDb
{
    /// <inheritdoc />
    public partial class ModifyAuditEntitytochangeIdtoGuiddatatype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Manually alter the columns using PostgreSQL's USING clause to handle the cast
            migrationBuilder.Sql("ALTER TABLE \"tenant\".\"tenants\" ALTER COLUMN \"modified_by\" TYPE uuid USING (NULLIF(\"modified_by\", '')::uuid);");
            migrationBuilder.Sql("ALTER TABLE \"tenant\".\"tenants\" ALTER COLUMN \"created_by\" TYPE uuid USING (NULLIF(\"created_by\", '')::uuid);");


            migrationBuilder.AlterColumn<Guid>(
                name: "modified_by",
                schema: "tenant",
                table: "tenants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by",
                schema: "tenant",
                table: "tenants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "modified_by",
                schema: "tenant",
                table: "tenants",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                schema: "tenant",
                table: "tenants",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
