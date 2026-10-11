using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant");

            migrationBuilder.CreateTable(
                name: "feature_flags",
                schema: "tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_flags", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flags_key_tenant_id",
                schema: "tenant",
                table: "feature_flags",
                columns: new[] { "key", "tenant_id" },
                unique: true);

            migrationBuilder.InsertData(
                schema: "tenant",
                table: "feature_flags",
                columns: new[] { "id", "key", "name", "description", "is_enabled", "category", "tenant_id", "created_at" },
                values: new object[]
                {
                    new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                    "voice_typing",
                    "Voice Typing & Speech Dictation",
                    "Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API",
                    true,
                    "Accessibility",
                    null,
                    new DateTimeOffset(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc))
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flags",
                schema: "tenant");
        }
    }
}

