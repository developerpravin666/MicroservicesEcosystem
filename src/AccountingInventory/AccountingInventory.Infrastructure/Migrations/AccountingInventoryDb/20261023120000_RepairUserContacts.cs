using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261023120000_RepairUserContacts")]
public sealed class RepairUserContacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older registration passed email and mobile to User.Create in reverse order.
        // Only repair unmistakably swapped pairs; leave ambiguous historical values for review.
        migrationBuilder.Sql("""
            UPDATE users SET email = LOWER(TRIM(mobile)), mobile = TRIM(email)
            WHERE mobile ~ '^[^[:space:]@]+@[^[:space:]@]+[.][^[:space:]@]+$'
              AND email ~ '^[+0-9() -]+$';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve corrected contact data on rollback.
    }
}
