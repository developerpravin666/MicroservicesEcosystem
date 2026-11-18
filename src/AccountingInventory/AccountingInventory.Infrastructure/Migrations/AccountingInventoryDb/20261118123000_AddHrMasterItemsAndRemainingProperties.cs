using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261118123000_AddHrMasterItemsAndRemainingProperties")]
public sealed class AddHrMasterItemsAndRemainingProperties : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- 1. Ensure remaining properties on hr_employees table
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS date_of_birth date;
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS gender character varying(20);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS salary_type character varying(30) DEFAULT 'Monthly';
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS reports_to_employee_id uuid;
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS reports_to_employee_name character varying(150);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS pan_number character varying(20);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS aadhaar_number character varying(30);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS uan_number character varying(30);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS bank_account_number character varying(50);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS bank_name character varying(100);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS bank_ifsc_code character varying(20);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS address text;
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS emergency_contact_name character varying(100);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS emergency_contact_phone character varying(30);
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS notes text;
            ALTER TABLE hr_employees ADD COLUMN IF NOT EXISTS monthly_base_salary numeric(18,2) NOT NULL DEFAULT 0.00;

            -- 2. Ensure remaining properties on hr_leave_requests table
            ALTER TABLE hr_leave_requests ADD COLUMN IF NOT EXISTS approver_comment text;
            ALTER TABLE hr_leave_requests ADD COLUMN IF NOT EXISTS approved_by character varying(150);
            ALTER TABLE hr_leave_requests ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone;

            -- 3. Ensure remaining properties on hr_salary_structures table
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS performance_bonus numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS tds_tax numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS other_deductions numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS payment_mode character varying(50) NOT NULL DEFAULT 'Bank Transfer';
            ALTER TABLE hr_salary_structures ADD COLUMN IF NOT EXISTS remarks text;

            -- 4. Ensure remaining properties on hr_payroll_runs table
            ALTER TABLE hr_payroll_runs ADD COLUMN IF NOT EXISTS payment_date date;
            ALTER TABLE hr_payroll_runs ADD COLUMN IF NOT EXISTS processed_by character varying(150);
            ALTER TABLE hr_payroll_runs ADD COLUMN IF NOT EXISTS notes text;

            -- 5. Ensure remaining properties on hr_payslips table
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS payment_status integer NOT NULL DEFAULT 1;
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS disbursed_at timestamp with time zone;
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS payment_reference character varying(100);
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS bonus numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS allowances numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS lop_deduction numeric(18,2) NOT NULL DEFAULT 0.00;
            ALTER TABLE hr_payslips ADD COLUMN IF NOT EXISTS other_deductions numeric(18,2) NOT NULL DEFAULT 0.00;

            -- 6. HR Master Items Table for Configurable Dictionaries
            CREATE TABLE IF NOT EXISTS hr_master_items (
                id uuid NOT NULL,
                category character varying(50) NOT NULL,
                name character varying(150) NOT NULL,
                code character varying(50) NOT NULL,
                description text,
                is_default boolean NOT NULL DEFAULT false,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_master_items PRIMARY KEY (id)
            );
            ALTER TABLE hr_master_items DROP COLUMN IF EXISTS sort_order;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_master_items_category_code 
                ON hr_master_items (category, code) WHERE NOT is_deleted;

            -- 7. Seed standard HR Masters with valid RFC 4122 v4 UUIDs
            DELETE FROM hr_master_items WHERE id::text LIKE 'a1a11111%' OR id::text LIKE 'm1a11111%';

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '5a0fb2b6-ac86-447f-9645-4ac5703d22bf', 'JOB_TITLE', 'Store Manager', 'STORE_MGR', 'Overall retail branch & inventory incharge', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'JOB_TITLE' AND code = 'STORE_MGR');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'bcf18748-b0d7-4876-937f-361f0a6d0f08', 'JOB_TITLE', 'Senior Sales Consultant', 'SR_SALES', 'Counter sales & smartphone consultant', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'JOB_TITLE' AND code = 'SR_SALES');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '02242351-6263-490e-86df-e5dfa81ed111', 'JOB_TITLE', 'Billing Cashier', 'CASHIER', 'POS counter billing and POS machine handler', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'JOB_TITLE' AND code = 'CASHIER');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '715ece93-b4b5-408c-aa74-dddfdd1d29b4', 'JOB_TITLE', 'Inventory & Stock Incharge', 'STOCK_MGR', 'Barcode verification, godown inward and dispatch', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'JOB_TITLE' AND code = 'STOCK_MGR');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '633c2a85-4a07-492a-8007-a46e420d3b4a', 'JOB_TITLE', 'Accountant & Tax Executive', 'ACCOUNTANT', 'GST bookkeeping and tally reconciliation', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'JOB_TITLE' AND code = 'ACCOUNTANT');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '980c2a4e-9dc5-4c5f-acc8-3dc3b4b5de96', 'EMPLOYMENT_TYPE', 'Full Time', 'FULL_TIME', 'Permanent monthly payroll staff', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'FULL_TIME');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'fdbc8cf5-6091-4fdc-a05c-b8e590c98419', 'EMPLOYMENT_TYPE', 'Part Time', 'PART_TIME', 'Hourly / partial shift workforce', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'PART_TIME');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '765716f3-a229-49bb-bd1d-9363e9f7ff3c', 'EMPLOYMENT_TYPE', 'Contract / Retainer', 'CONTRACT', 'Third-party agency or contracted staff', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'CONTRACT');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '76061f66-2271-494e-94a9-deee030fab75', 'EMPLOYMENT_TYPE', 'Trainee / Intern', 'INTERN', 'Stipend-based trainee period', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'INTERN');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'b6769b65-21a3-43a9-8b61-a0fdc0743619', 'EMPLOYEE_STATUS', 'Active', 'ACTIVE', 'Active on payroll and store shifts', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'ACTIVE');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'd41a9627-23a8-41ba-945a-c8741ddb64cd', 'EMPLOYEE_STATUS', 'Probation', 'PROBATION', 'New recruit under 3-6 months evaluation', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'PROBATION');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '0713e84c-cfd1-4c80-b41b-9d1ba7f39ffc', 'EMPLOYEE_STATUS', 'On Leave', 'ON_LEAVE', 'Approved extended or maternity leave', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'ON_LEAVE');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'd6c5fca9-06f2-4603-a1f1-9ed65fa409a3', 'EMPLOYEE_STATUS', 'Resigned', 'RESIGNED', 'Serving notice period', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'RESIGNED');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'e6fead26-6035-4100-875f-36e92730f2e3', 'EMPLOYEE_STATUS', 'Terminated', 'TERMINATED', 'Employment officially ended', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'TERMINATED');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT 'c63e827d-0d01-47c5-9f52-7ddb3484e6d4', 'SALARY_TYPE', 'Monthly Gross Salary', 'Monthly', 'Direct gross salary paid on monthly cycle', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'SALARY_TYPE' AND code = 'Monthly');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '6d579f46-bdfa-4472-9409-6cf8dff47a44', 'SALARY_TYPE', 'Annual Cost to Company (CTC)', 'YearlyPackage', 'Total annual cost inclusive of statutory bonuses & PF', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'SALARY_TYPE' AND code = 'YearlyPackage');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '1e97d014-37e9-4dfc-a5bb-f44f75433117', 'LEAVE_CATEGORY', 'General Paid Leave', 'PAID', 'Eligible for normal salary disbursement', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'PAID');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '5dee2a6f-a483-47e4-b538-b747577cca7a', 'LEAVE_CATEGORY', 'Medical / Certified', 'MEDICAL', 'Requires doctor note or certificate', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'MEDICAL');

            INSERT INTO hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
            SELECT '99e6def0-22b4-42bf-9b41-23c063ab6f93', 'LEAVE_CATEGORY', 'Loss of Pay / Unpaid', 'UNPAID', 'Deducted directly from monthly attendance', false, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'UNPAID');
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS hr_master_items;
        """);
    }
}
