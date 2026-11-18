using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261118153000_AddHouseRentAllowanceToSalaryStructureAndPayslips")]
public sealed class AddHouseRentAllowanceToSalaryStructureAndPayslips : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Ensure house_rent_allowance column exists on hr_salary_structures
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS house_rent_allowance numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_salary_structures SET house_rent_allowance = hra WHERE house_rent_allowance = 0.00 AND hra > 0.00;

            -- Ensure house_rent_allowance column exists on hr_payslips
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS house_rent_allowance numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_payslips SET house_rent_allowance = hra WHERE house_rent_allowance = 0.00 AND hra > 0.00;
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE hr_salary_structures DROP COLUMN IF EXISTS house_rent_allowance;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS house_rent_allowance;
        """);
    }
}
