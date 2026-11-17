using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261117191500_AddSmartHrPayrollLeaveTables")]
public sealed class AddSmartHrPayrollLeaveTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- 1. Departments Master
            CREATE TABLE IF NOT EXISTS hr_departments (
                id uuid NOT NULL,
                name character varying(100) NOT NULL,
                code character varying(50) NOT NULL,
                description text,
                head_of_department character varying(150),
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_departments PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_departments_code ON hr_departments (code) WHERE NOT is_deleted;

            -- 2. Employees Master
            CREATE TABLE IF NOT EXISTS hr_employees (
                id uuid NOT NULL,
                employee_code character varying(50) NOT NULL,
                first_name character varying(100) NOT NULL,
                last_name character varying(100) NOT NULL,
                email character varying(150),
                phone character varying(30) NOT NULL,
                department character varying(100),
                designation character varying(150) NOT NULL,
                date_of_joining date NOT NULL,
                date_of_birth date,
                gender character varying(20),
                employment_type integer NOT NULL DEFAULT 1,
                employment_status integer NOT NULL DEFAULT 1,
                pan_number character varying(20),
                aadhaar_number character varying(30),
                uan_number character varying(30),
                bank_account_number character varying(50),
                bank_name character varying(100),
                bank_ifsc_code character varying(20),
                monthly_base_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                address text,
                emergency_contact_name character varying(100),
                emergency_contact_phone character varying(30),
                notes text,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_employees PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_employees_employee_code ON hr_employees (employee_code) WHERE NOT is_deleted;
            CREATE INDEX IF NOT EXISTS ix_hr_employees_department ON hr_employees (department);
            CREATE INDEX IF NOT EXISTS ix_hr_employees_employment_status ON hr_employees (employment_status);

            -- 3. Leave Types Master
            CREATE TABLE IF NOT EXISTS hr_leave_types (
                id uuid NOT NULL,
                name character varying(100) NOT NULL,
                code character varying(20) NOT NULL,
                days_allowed_per_year integer NOT NULL DEFAULT 12,
                is_paid boolean NOT NULL DEFAULT true,
                is_carry_forward boolean NOT NULL DEFAULT false,
                description text,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_leave_types PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_leave_types_code ON hr_leave_types (code) WHERE NOT is_deleted;

            -- 4. Leave Balances
            CREATE TABLE IF NOT EXISTS hr_leave_balances (
                id uuid NOT NULL,
                employee_id uuid NOT NULL,
                employee_code character varying(50) NOT NULL,
                employee_name character varying(150) NOT NULL,
                leave_type_id uuid NOT NULL,
                leave_type_name character varying(100) NOT NULL,
                year integer NOT NULL,
                total_allocated numeric(8,2) NOT NULL DEFAULT 0.00,
                used_days numeric(8,2) NOT NULL DEFAULT 0.00,
                pending_approval_days numeric(8,2) NOT NULL DEFAULT 0.00,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_leave_balances PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_leave_balances_employee_type_year ON hr_leave_balances (employee_id, leave_type_id, year) WHERE NOT is_deleted;

            -- 5. Leave Requests
            CREATE TABLE IF NOT EXISTS hr_leave_requests (
                id uuid NOT NULL,
                employee_id uuid NOT NULL,
                employee_code character varying(50) NOT NULL,
                employee_name character varying(150) NOT NULL,
                leave_type_id uuid NOT NULL,
                leave_type_name character varying(100) NOT NULL,
                start_date date NOT NULL,
                end_date date NOT NULL,
                total_days numeric(8,2) NOT NULL DEFAULT 1.00,
                reason character varying(500) NOT NULL,
                status integer NOT NULL DEFAULT 1,
                approver_comment text,
                approved_by character varying(150),
                approved_at timestamp with time zone,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_leave_requests PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_hr_leave_requests_employee_id ON hr_leave_requests (employee_id);
            CREATE INDEX IF NOT EXISTS ix_hr_leave_requests_status ON hr_leave_requests (status);

            -- 6. Salary Structures
            CREATE TABLE IF NOT EXISTS hr_salary_structures (
                id uuid NOT NULL,
                employee_id uuid NOT NULL,
                employee_code character varying(50) NOT NULL,
                employee_name character varying(150) NOT NULL,
                basic_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                hra numeric(18,2) NOT NULL DEFAULT 0.00,
                dearness_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
                conveyance_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
                special_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
                medical_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
                performance_bonus numeric(18,2) NOT NULL DEFAULT 0.00,
                provident_fund numeric(18,2) NOT NULL DEFAULT 0.00,
                esi numeric(18,2) NOT NULL DEFAULT 0.00,
                professional_tax numeric(18,2) NOT NULL DEFAULT 0.00,
                tds_tax numeric(18,2) NOT NULL DEFAULT 0.00,
                other_deductions numeric(18,2) NOT NULL DEFAULT 0.00,
                payment_mode character varying(50) NOT NULL DEFAULT 'Bank Transfer',
                remarks text,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_salary_structures PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_salary_structures_employee_id ON hr_salary_structures (employee_id) WHERE NOT is_deleted;

            -- 7. Payroll Runs
            CREATE TABLE IF NOT EXISTS hr_payroll_runs (
                id uuid NOT NULL,
                month integer NOT NULL,
                year integer NOT NULL,
                run_date date NOT NULL,
                total_employees integer NOT NULL DEFAULT 0,
                total_gross_amount numeric(18,2) NOT NULL DEFAULT 0.00,
                total_deductions numeric(18,2) NOT NULL DEFAULT 0.00,
                total_net_amount numeric(18,2) NOT NULL DEFAULT 0.00,
                status integer NOT NULL DEFAULT 1,
                payment_date date,
                processed_by character varying(150),
                notes text,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_payroll_runs PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_hr_payroll_runs_month_year ON hr_payroll_runs (month, year);
            CREATE INDEX IF NOT EXISTS ix_hr_payroll_runs_status ON hr_payroll_runs (status);

            -- 8. Payslips
            CREATE TABLE IF NOT EXISTS hr_payslips (
                id uuid NOT NULL,
                payroll_run_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                employee_code character varying(50) NOT NULL,
                employee_name character varying(150) NOT NULL,
                department character varying(100),
                designation character varying(150) NOT NULL,
                month integer NOT NULL,
                year integer NOT NULL,
                working_days integer NOT NULL DEFAULT 30,
                present_days integer NOT NULL DEFAULT 30,
                paid_leave_days numeric(8,2) NOT NULL DEFAULT 0.00,
                unpaid_leave_days numeric(8,2) NOT NULL DEFAULT 0.00,
                basic_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                hra numeric(18,2) NOT NULL DEFAULT 0.00,
                allowances numeric(18,2) NOT NULL DEFAULT 0.00,
                bonus numeric(18,2) NOT NULL DEFAULT 0.00,
                gross_earnings numeric(18,2) NOT NULL DEFAULT 0.00,
                pf_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                esi_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                professional_tax numeric(18,2) NOT NULL DEFAULT 0.00,
                tds_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                lop_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                other_deductions numeric(18,2) NOT NULL DEFAULT 0.00,
                total_deductions numeric(18,2) NOT NULL DEFAULT 0.00,
                net_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                payment_status integer NOT NULL DEFAULT 1,
                disbursed_at timestamp with time zone,
                payment_reference character varying(100),
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_hr_payslips PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_hr_payslips_payroll_run_id ON hr_payslips (payroll_run_id);
            CREATE INDEX IF NOT EXISTS ix_hr_payslips_employee_id ON hr_payslips (employee_id);
            CREATE INDEX IF NOT EXISTS ix_hr_payslips_payment_status ON hr_payslips (payment_status);

            -- Default Leave Types Seed
            INSERT INTO hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
            VALUES
                ('e1a11111-0000-0000-0000-000000000001', 'Casual Leave', 'CL', 12, true, false, 'Standard annual casual leave for personal work', true, false, NOW()),
                ('e1a11111-0000-0000-0000-000000000002', 'Sick Leave', 'SL', 10, true, false, 'Medical and sickness leave with certificate verification', true, false, NOW()),
                ('e1a11111-0000-0000-0000-000000000003', 'Earned Leave', 'EL', 15, true, true, 'Privilege/Earned leaves eligible for carry-forward and encashment', true, false, NOW()),
                ('e1a11111-0000-0000-0000-000000000004', 'Loss of Pay', 'LOP', 0, false, false, 'Unpaid leave where salary deduction applies automatically', true, false, NOW())
            ON CONFLICT DO NOTHING;

            -- Default Departments Seed
            INSERT INTO hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
            VALUES
                ('d1a11111-0000-0000-0000-000000000001', 'Sales & Counter Billing', 'SALES', 'Retail counter sales, mobile retail consultants & billing cashiers', 'Store Manager', true, false, NOW()),
                ('d1a11111-0000-0000-0000-000000000002', 'Inventory & Warehouse', 'INVENTORY', 'Device stocking, godown receiving, barcode allocation & dispatch', 'Warehouse Lead', true, false, NOW()),
                ('d1a11111-0000-0000-0000-000000000003', 'Accounts & Finance', 'ACCOUNTS', 'GST compliance, tally reconciliation, vendor payments & payroll ledger', 'Chief Accountant', true, false, NOW()),
                ('d1a11111-0000-0000-0000-000000000004', 'Administration & HR', 'ADMIN', 'Store operations, staff management, onboarding & compliance', 'Operations Manager', true, false, NOW())
            ON CONFLICT DO NOTHING;
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS hr_payslips;
            DROP TABLE IF EXISTS hr_payroll_runs;
            DROP TABLE IF EXISTS hr_salary_structures;
            DROP TABLE IF EXISTS hr_leave_requests;
            DROP TABLE IF EXISTS hr_leave_balances;
            DROP TABLE IF EXISTS hr_leave_types;
            DROP TABLE IF EXISTS hr_employees;
            DROP TABLE IF EXISTS hr_departments;
        """);
    }
}
