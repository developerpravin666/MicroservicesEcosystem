using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "tenant",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gstin",
                schema: "tenant",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "initial_password_hash",
                schema: "tenant",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_email",
                schema: "tenant",
                table: "tenants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_mobile",
                schema: "tenant",
                table: "tenants",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_name",
                schema: "tenant",
                table: "tenants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "tenant",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state_code",
                schema: "tenant",
                table: "tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "gstin",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "initial_password_hash",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_email",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_mobile",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_name",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "tenant",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "state_code",
                schema: "tenant",
                table: "tenants");
        }
    }
}

