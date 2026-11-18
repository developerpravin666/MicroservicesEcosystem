using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261118160000_AddProvidentFundAndDeductionAliasesToHrPayslips")]
public sealed class AddProvidentFundAndDeductionAliasesToHrPayslips : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Ensure provident_fund and deduction alias columns exist on hr_payslips
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS provident_fund numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_payslips SET provident_fund = pf_deduction WHERE provident_fund = 0.00 AND pf_deduction > 0.00;

            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS employee_state_insurance numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_payslips SET employee_state_insurance = esi_deduction WHERE employee_state_insurance = 0.00 AND esi_deduction > 0.00;

            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS tax_deducted_at_source numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_payslips SET tax_deducted_at_source = tds_deduction WHERE tax_deducted_at_source = 0.00 AND tds_deduction > 0.00;

            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS loss_of_pay_deduction numeric(18,2) NOT NULL DEFAULT 0.00;
            UPDATE hr_payslips SET loss_of_pay_deduction = lop_deduction WHERE loss_of_pay_deduction = 0.00 AND lop_deduction > 0.00;

            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS total_working_days integer NOT NULL DEFAULT 30;
            UPDATE hr_payslips SET total_working_days = working_days WHERE total_working_days = 30 AND working_days <> 30;

            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS department_name character varying(150);
            UPDATE hr_payslips SET department_name = department WHERE department_name IS NULL AND department IS NOT NULL;
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS provident_fund;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS employee_state_insurance;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS tax_deducted_at_source;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS loss_of_pay_deduction;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS total_working_days;
            ALTER TABLE hr_payslips DROP COLUMN IF EXISTS department_name;
        """);
    }
}
