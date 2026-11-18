using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace AccountingInventory.Infrastructure.Persistence;

internal static class TenantSchemaMigrator
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> SchemaLocks = new();

    public static async Task MigrateAsync(
        string connectionString,
        string schemaName,
        CancellationToken cancellationToken)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);
        var semaphore = SchemaLocks.GetOrAdd(schemaName, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var tenantProvider = new StaticTenantProvider(schemaName);
            var tenantConnectionString = CreateTenantConnectionString(connectionString, schemaName);

            var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
                .UseNpgsql(
                    tenantConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__TenantSchemaHistory", tenantProvider.SchemaName))
                .UseSnakeCaseNamingConvention()
                .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using var applicationDbContext = new AccountingInventoryDbContext(options, tenantProvider);

            // PostgreSQL identifiers cannot be parameters. Validation guarantees this
            // value is a valid tenant-schema identifier before it is interpolated.
            await applicationDbContext.Database.ExecuteSqlRawAsync(
                $"CREATE SCHEMA IF NOT EXISTS \"{tenantProvider.SchemaName}\";", cancellationToken);
            await applicationDbContext.Database.MigrateAsync(cancellationToken);

        // Idempotent safety fallback executed inside an explicit database transaction:
        // ensures staging tables and indexes exist even if historical schemas were bootstrapped
        // without full migration execution, and syncs table registry for this tenant schema.
        await using var tx = await applicationDbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await applicationDbContext.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".master_import_batches (
                    id uuid NOT NULL,
                    batch_number character varying(80) NOT NULL,
                    file_name character varying(250) NOT NULL,
                    file_type character varying(50),
                    status integer NOT NULL,
                    total_rows integer NOT NULL,
                    valid_rows integer NOT NULL,
                    error_rows integer NOT NULL,
                    warning_rows integer NOT NULL,
                    created_count integer NOT NULL,
                    updated_count integer NOT NULL,
                    summary_json text,
                    review_notes text,
                    reviewed_at timestamp with time zone,
                    reviewed_by text,
                    created_at timestamp with time zone NOT NULL,
                    created_by uuid,
                    modified_at timestamp with time zone,
                    modified_by uuid,
                    is_active boolean NOT NULL,
                    is_deleted boolean NOT NULL,
                    CONSTRAINT pk_master_import_batches PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_master_import_batches_batch_number
                    ON ""{tenantProvider.SchemaName}"".master_import_batches (batch_number);

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".master_import_staging_rows (
                    id uuid NOT NULL,
                    batch_id uuid NOT NULL,
                    row_index integer NOT NULL,
                    entity_type character varying(50) NOT NULL,
                    action integer NOT NULL,
                    status integer NOT NULL,
                    entity_key character varying(150),
                    entity_name character varying(250),
                    validation_errors text,
                    raw_data_json text NOT NULL,
                    is_approved boolean NOT NULL,
                    is_imported boolean NOT NULL,
                    imported_at timestamp with time zone,
                    import_message text,
                    created_at timestamp with time zone NOT NULL,
                    created_by uuid,
                    modified_at timestamp with time zone,
                    modified_by uuid,
                    is_active boolean NOT NULL,
                    is_deleted boolean NOT NULL,
                    CONSTRAINT pk_master_import_staging_rows PRIMARY KEY (id)
                );

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint 
                        WHERE conname = 'fk_master_import_staging_rows_master_import_batches_batch_id'
                          AND conrelid = '""{tenantProvider.SchemaName}"".master_import_staging_rows'::regclass
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".master_import_staging_rows
                            ADD CONSTRAINT fk_master_import_staging_rows_master_import_batches_batch_id
                            FOREIGN KEY (batch_id) REFERENCES ""{tenantProvider.SchemaName}"".master_import_batches (id) ON DELETE CASCADE;
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS ix_master_import_staging_rows_batch_id_row_index
                    ON ""{tenantProvider.SchemaName}"".master_import_staging_rows (batch_id, row_index);

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'vendors'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".vendors 
                            ADD COLUMN IF NOT EXISTS gst_number character varying(20);
                    END IF;
                END $$;

                -- Idempotent verification for HR + Leave + Payroll tables across all tenant schemas
                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_departments (
                    id uuid NOT NULL PRIMARY KEY,
                    name character varying(100) NOT NULL,
                    code character varying(50) NOT NULL,
                    description text,
                    head_of_department character varying(150),
                    is_active boolean NOT NULL DEFAULT true,
                    is_deleted boolean NOT NULL DEFAULT false,
                    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                    created_by uuid,
                    modified_at timestamp with time zone,
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_departments_code 
                    ON ""{tenantProvider.SchemaName}"".hr_departments (code) WHERE NOT is_deleted;

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_employees (
                    id uuid NOT NULL PRIMARY KEY,
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
                    salary_type character varying(30) DEFAULT 'Monthly',
                    reports_to_employee_id uuid,
                    reports_to_employee_name character varying(150),
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
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_employees_employee_code 
                    ON ""{tenantProvider.SchemaName}"".hr_employees (employee_code) WHERE NOT is_deleted;

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_leave_types (
                    id uuid NOT NULL PRIMARY KEY,
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
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_leave_types_code 
                    ON ""{tenantProvider.SchemaName}"".hr_leave_types (code) WHERE NOT is_deleted;

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_leave_balances (
                    id uuid NOT NULL PRIMARY KEY,
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
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_leave_balances_employee_type_year 
                    ON ""{tenantProvider.SchemaName}"".hr_leave_balances (employee_id, leave_type_id, year) WHERE NOT is_deleted;

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_leave_requests (
                    id uuid NOT NULL PRIMARY KEY,
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
                    modified_by uuid
                );

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_salary_structures (
                    id uuid NOT NULL PRIMARY KEY,
                    employee_id uuid NOT NULL,
                    employee_code character varying(50) NOT NULL,
                    employee_name character varying(150) NOT NULL,
                    basic_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                    house_rent_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
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
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_salary_structures_employee_id 
                    ON ""{tenantProvider.SchemaName}"".hr_salary_structures (employee_id) WHERE NOT is_deleted;

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_payroll_runs (
                    id uuid NOT NULL PRIMARY KEY,
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
                    modified_by uuid
                );

                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_payslips (
                    id uuid NOT NULL PRIMARY KEY,
                    payroll_run_id uuid NOT NULL,
                    employee_id uuid NOT NULL,
                    employee_code character varying(50) NOT NULL,
                    employee_name character varying(150) NOT NULL,
                    department character varying(100),
                    department_name character varying(150),
                    designation character varying(150) NOT NULL,
                    month integer NOT NULL,
                    year integer NOT NULL,
                    working_days integer NOT NULL DEFAULT 30,
                    total_working_days integer NOT NULL DEFAULT 30,
                    present_days integer NOT NULL DEFAULT 30,
                    paid_leave_days numeric(8,2) NOT NULL DEFAULT 0.00,
                    unpaid_leave_days numeric(8,2) NOT NULL DEFAULT 0.00,
                    basic_salary numeric(18,2) NOT NULL DEFAULT 0.00,
                    house_rent_allowance numeric(18,2) NOT NULL DEFAULT 0.00,
                    hra numeric(18,2) NOT NULL DEFAULT 0.00,
                    allowances numeric(18,2) NOT NULL DEFAULT 0.00,
                    bonus numeric(18,2) NOT NULL DEFAULT 0.00,
                    gross_earnings numeric(18,2) NOT NULL DEFAULT 0.00,
                    provident_fund numeric(18,2) NOT NULL DEFAULT 0.00,
                    pf_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                    employee_state_insurance numeric(18,2) NOT NULL DEFAULT 0.00,
                    esi_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                    professional_tax numeric(18,2) NOT NULL DEFAULT 0.00,
                    tax_deducted_at_source numeric(18,2) NOT NULL DEFAULT 0.00,
                    tds_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
                    loss_of_pay_deduction numeric(18,2) NOT NULL DEFAULT 0.00,
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
                    modified_by uuid
                );

                -- Fix legacy fake patterned GUIDs to valid RFC 4122 v4 UUIDs
                UPDATE ""{tenantProvider.SchemaName}"".hr_leave_types SET id = '69b3d1bb-b24a-4316-b591-60ef2ea933e5' WHERE code = 'CL' AND id != '69b3d1bb-b24a-4316-b591-60ef2ea933e5';
                UPDATE ""{tenantProvider.SchemaName}"".hr_leave_types SET id = 'aca66ea5-c58f-41f0-8ec3-4690743bccb8' WHERE code = 'SL' AND id != 'aca66ea5-c58f-41f0-8ec3-4690743bccb8';
                UPDATE ""{tenantProvider.SchemaName}"".hr_leave_types SET id = 'df8d9baf-eea2-42da-9a7e-f20194723076' WHERE code = 'EL' AND id != 'df8d9baf-eea2-42da-9a7e-f20194723076';
                UPDATE ""{tenantProvider.SchemaName}"".hr_leave_types SET id = 'daa2990a-e0df-42ec-b0b7-9a44d826f656' WHERE code = 'LOP' AND id != 'daa2990a-e0df-42ec-b0b7-9a44d826f656';

                -- Default leave types seed per tenant schema (RFC 4122 v4 UUIDs)
                INSERT INTO ""{tenantProvider.SchemaName}"".hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
                SELECT '69b3d1bb-b24a-4316-b591-60ef2ea933e5', 'Casual Leave', 'CL', 12, true, false, 'Standard annual casual leave for personal work', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_leave_types WHERE code = 'CL');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
                SELECT 'aca66ea5-c58f-41f0-8ec3-4690743bccb8', 'Sick Leave', 'SL', 10, true, false, 'Medical and sickness leave with certificate verification', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_leave_types WHERE code = 'SL');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
                SELECT 'df8d9baf-eea2-42da-9a7e-f20194723076', 'Earned Leave', 'EL', 15, true, true, 'Privilege/Earned leaves eligible for carry-forward and encashment', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_leave_types WHERE code = 'EL');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
                SELECT 'daa2990a-e0df-42ec-b0b7-9a44d826f656', 'Loss of Pay', 'LOP', 0, false, false, 'Unpaid leave where salary deduction applies automatically', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_leave_types WHERE code = 'LOP');

                -- Fix legacy fake patterned GUIDs to valid RFC 4122 v4 UUIDs
                UPDATE ""{tenantProvider.SchemaName}"".hr_departments SET id = '0cddb3ae-b316-4fbb-a42f-030b3288fbe1' WHERE code = 'SALES' AND id != '0cddb3ae-b316-4fbb-a42f-030b3288fbe1';
                UPDATE ""{tenantProvider.SchemaName}"".hr_departments SET id = '3b218440-954f-4536-8bcc-883f1159bdca' WHERE code = 'INVENTORY' AND id != '3b218440-954f-4536-8bcc-883f1159bdca';
                UPDATE ""{tenantProvider.SchemaName}"".hr_departments SET id = '959b265c-f347-4bf0-a6c0-e1055a6c4fe4' WHERE code = 'ACCOUNTS' AND id != '959b265c-f347-4bf0-a6c0-e1055a6c4fe4';
                UPDATE ""{tenantProvider.SchemaName}"".hr_departments SET id = 'e921619f-d60d-4e21-8ef3-f8ce05198ace' WHERE code = 'ADMIN' AND id != 'e921619f-d60d-4e21-8ef3-f8ce05198ace';

                -- Default departments seed per tenant schema (RFC 4122 v4 UUIDs)
                INSERT INTO ""{tenantProvider.SchemaName}"".hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
                SELECT '0cddb3ae-b316-4fbb-a42f-030b3288fbe1', 'Sales & Counter Billing', 'SALES', 'Retail counter sales, mobile retail consultants & billing cashiers', 'Store Manager', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_departments WHERE code = 'SALES');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
                SELECT '3b218440-954f-4536-8bcc-883f1159bdca', 'Inventory & Warehouse', 'INVENTORY', 'Device stocking, godown receiving, barcode allocation & dispatch', 'Warehouse Lead', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_departments WHERE code = 'INVENTORY');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
                SELECT '959b265c-f347-4bf0-a6c0-e1055a6c4fe4', 'Accounts & Finance', 'ACCOUNTS', 'GST compliance, tally reconciliation, vendor payments & payroll ledger', 'Chief Accountant', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_departments WHERE code = 'ACCOUNTS');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
                SELECT 'e921619f-d60d-4e21-8ef3-f8ce05198ace', 'Administration & HR', 'ADMIN', 'Store operations, staff management, onboarding & compliance', 'Operations Manager', true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_departments WHERE code = 'ADMIN');

                -- Ensure new columns exist on HR tables for existing tenant schemas
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_employees'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_employees 
                            ADD COLUMN IF NOT EXISTS date_of_birth date,
                            ADD COLUMN IF NOT EXISTS gender character varying(20),
                            ADD COLUMN IF NOT EXISTS salary_type character varying(30) DEFAULT 'Monthly',
                            ADD COLUMN IF NOT EXISTS reports_to_employee_id uuid,
                            ADD COLUMN IF NOT EXISTS reports_to_employee_name character varying(150),
                            ADD COLUMN IF NOT EXISTS bank_account_number character varying(50),
                            ADD COLUMN IF NOT EXISTS bank_name character varying(100),
                            ADD COLUMN IF NOT EXISTS bank_ifsc_code character varying(20),
                            ADD COLUMN IF NOT EXISTS pan_number character varying(20),
                            ADD COLUMN IF NOT EXISTS aadhaar_number character varying(30),
                            ADD COLUMN IF NOT EXISTS uan_number character varying(30),
                            ADD COLUMN IF NOT EXISTS address text,
                            ADD COLUMN IF NOT EXISTS emergency_contact_name character varying(100),
                            ADD COLUMN IF NOT EXISTS emergency_contact_phone character varying(30),
                            ADD COLUMN IF NOT EXISTS notes text,
                            ADD COLUMN IF NOT EXISTS monthly_base_salary numeric(18,2) DEFAULT 0.00;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_leave_requests'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_leave_requests 
                            ADD COLUMN IF NOT EXISTS approver_comment text,
                            ADD COLUMN IF NOT EXISTS approved_by character varying(150),
                            ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_salary_structures'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_salary_structures 
                            ADD COLUMN IF NOT EXISTS house_rent_allowance numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS performance_bonus numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS tds_tax numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS other_deductions numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS payment_mode character varying(50) DEFAULT 'Bank Transfer',
                            ADD COLUMN IF NOT EXISTS remarks text;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_salary_structures 
                            SET house_rent_allowance = hra 
                            WHERE house_rent_allowance = 0.00 AND hra > 0.00;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_payroll_runs'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_payroll_runs 
                            ADD COLUMN IF NOT EXISTS payment_date date,
                            ADD COLUMN IF NOT EXISTS processed_by character varying(150),
                            ADD COLUMN IF NOT EXISTS notes text;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_payslips'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_payslips 
                            ADD COLUMN IF NOT EXISTS house_rent_allowance numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS provident_fund numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS employee_state_insurance numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS tax_deducted_at_source numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS loss_of_pay_deduction numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS total_working_days integer DEFAULT 30,
                            ADD COLUMN IF NOT EXISTS department_name character varying(150),
                            ADD COLUMN IF NOT EXISTS payment_status integer DEFAULT 1,
                            ADD COLUMN IF NOT EXISTS disbursed_at timestamp with time zone,
                            ADD COLUMN IF NOT EXISTS payment_reference character varying(100),
                            ADD COLUMN IF NOT EXISTS bonus numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS allowances numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS lop_deduction numeric(18,2) DEFAULT 0.00,
                            ADD COLUMN IF NOT EXISTS other_deductions numeric(18,2) DEFAULT 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET house_rent_allowance = hra 
                            WHERE house_rent_allowance = 0.00 AND hra > 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET provident_fund = pf_deduction 
                            WHERE provident_fund = 0.00 AND pf_deduction > 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET employee_state_insurance = esi_deduction 
                            WHERE employee_state_insurance = 0.00 AND esi_deduction > 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET tax_deducted_at_source = tds_deduction 
                            WHERE tax_deducted_at_source = 0.00 AND tds_deduction > 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET loss_of_pay_deduction = lop_deduction 
                            WHERE loss_of_pay_deduction = 0.00 AND lop_deduction > 0.00;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET total_working_days = working_days 
                            WHERE total_working_days = 30 AND working_days <> 30;

                        UPDATE ""{tenantProvider.SchemaName}"".hr_payslips 
                            SET department_name = department 
                            WHERE department_name IS NULL AND department IS NOT NULL;
                    END IF;
                END $$;

                -- Master Items Table for HR & Payroll
                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_master_items (
                    id uuid NOT NULL PRIMARY KEY,
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
                    modified_by uuid
                );
                ALTER TABLE ""{tenantProvider.SchemaName}"".hr_master_items DROP COLUMN IF EXISTS sort_order;
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_master_items_category_code 
                    ON ""{tenantProvider.SchemaName}"".hr_master_items (category, code) WHERE NOT is_deleted;

                -- Clean legacy fake GUIDs and seed standard HR Masters (RFC 4122 v4 UUIDs)
                DELETE FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE id::text LIKE 'a1a11111%' OR id::text LIKE 'm1a11111%';

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '5a0fb2b6-ac86-447f-9645-4ac5703d22bf', 'JOB_TITLE', 'Store Manager', 'STORE_MGR', 'Overall retail branch & inventory incharge', true, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'JOB_TITLE' AND code = 'STORE_MGR');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'bcf18748-b0d7-4876-937f-361f0a6d0f08', 'JOB_TITLE', 'Senior Sales Consultant', 'SR_SALES', 'Counter sales & smartphone consultant', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'JOB_TITLE' AND code = 'SR_SALES');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '02242351-6263-490e-86df-e5dfa81ed111', 'JOB_TITLE', 'Billing Cashier', 'CASHIER', 'POS counter billing and POS machine handler', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'JOB_TITLE' AND code = 'CASHIER');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '715ece93-b4b5-408c-aa74-dddfdd1d29b4', 'JOB_TITLE', 'Inventory & Stock Incharge', 'STOCK_MGR', 'Barcode verification, godown inward and dispatch', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'JOB_TITLE' AND code = 'STOCK_MGR');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '633c2a85-4a07-492a-8007-a46e420d3b4a', 'JOB_TITLE', 'Accountant & Tax Executive', 'ACCOUNTANT', 'GST bookkeeping and tally reconciliation', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'JOB_TITLE' AND code = 'ACCOUNTANT');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '980c2a4e-9dc5-4c5f-acc8-3dc3b4b5de96', 'EMPLOYMENT_TYPE', 'Full Time', 'FULL_TIME', 'Permanent monthly payroll staff', true, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'FULL_TIME');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'fdbc8cf5-6091-4fdc-a05c-b8e590c98419', 'EMPLOYMENT_TYPE', 'Part Time', 'PART_TIME', 'Hourly / partial shift workforce', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'PART_TIME');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '765716f3-a229-49bb-bd1d-9363e9f7ff3c', 'EMPLOYMENT_TYPE', 'Contract / Retainer', 'CONTRACT', 'Third-party agency or contracted staff', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'CONTRACT');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '76061f66-2271-494e-94a9-deee030fab75', 'EMPLOYMENT_TYPE', 'Trainee / Intern', 'INTERN', 'Stipend-based trainee period', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYMENT_TYPE' AND code = 'INTERN');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'b6769b65-21a3-43a9-8b61-a0fdc0743619', 'EMPLOYEE_STATUS', 'Active', 'ACTIVE', 'Active on payroll and store shifts', true, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'ACTIVE');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'd41a9627-23a8-41ba-945a-c8741ddb64cd', 'EMPLOYEE_STATUS', 'Probation', 'PROBATION', 'New recruit under 3-6 months evaluation', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'PROBATION');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '0713e84c-cfd1-4c80-b41b-9d1ba7f39ffc', 'EMPLOYEE_STATUS', 'On Leave', 'ON_LEAVE', 'Approved extended or maternity leave', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'ON_LEAVE');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'd6c5fca9-06f2-4603-a1f1-9ed65fa409a3', 'EMPLOYEE_STATUS', 'Resigned', 'RESIGNED', 'Serving notice period', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'RESIGNED');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'e6fead26-6035-4100-875f-36e92730f2e3', 'EMPLOYEE_STATUS', 'Terminated', 'TERMINATED', 'Employment officially ended', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'EMPLOYEE_STATUS' AND code = 'TERMINATED');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT 'c63e827d-0d01-47c5-9f52-7ddb3484e6d4', 'SALARY_TYPE', 'Monthly Gross Salary', 'Monthly', 'Direct gross salary paid on monthly cycle', true, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'SALARY_TYPE' AND code = 'Monthly');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '6d579f46-bdfa-4472-9409-6cf8dff47a44', 'SALARY_TYPE', 'Annual Cost to Company (CTC)', 'YearlyPackage', 'Total annual cost inclusive of statutory bonuses & PF', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'SALARY_TYPE' AND code = 'YearlyPackage');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '1e97d014-37e9-4dfc-a5bb-f44f75433117', 'LEAVE_CATEGORY', 'General Paid Leave', 'PAID', 'Eligible for normal salary disbursement', true, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'PAID');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '5dee2a6f-a483-47e4-b538-b747577cca7a', 'LEAVE_CATEGORY', 'Medical / Certified', 'MEDICAL', 'Requires doctor note or certificate', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'MEDICAL');

                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, is_default, is_active, is_deleted, created_at)
                SELECT '99e6def0-22b4-42bf-9b41-23c063ab6f93', 'LEAVE_CATEGORY', 'Loss of Pay / Unpaid', 'UNPAID', 'Deducted directly from monthly attendance', false, true, false, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""{tenantProvider.SchemaName}"".hr_master_items WHERE category = 'LEAVE_CATEGORY' AND code = 'UNPAID');

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_proc p 
                        JOIN pg_namespace n ON p.pronamespace = n.oid 
                        WHERE n.nspname = 'tenant' AND p.proname = 'sync_table_registry'
                    ) THEN
                        PERFORM tenant.sync_table_registry('{tenantProvider.SchemaName}');
                    END IF;
                END $$;
            ", cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
    finally
    {
        semaphore.Release();
    }
}

    internal static string CreateTenantConnectionString(string connectionString, string schemaName)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Include public for extensions/functions while ensuring unqualified
            // EF DDL and DML always resolve to this tenant first.
            SearchPath = $"{schemaName},public"
        };

        return builder.ConnectionString;
    }
}
