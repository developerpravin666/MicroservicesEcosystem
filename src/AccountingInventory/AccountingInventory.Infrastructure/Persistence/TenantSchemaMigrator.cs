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
                    modified_by uuid
                );

                -- Default leave types seed per tenant schema
                INSERT INTO ""{tenantProvider.SchemaName}"".hr_leave_types (id, name, code, days_allowed_per_year, is_paid, is_carry_forward, description, is_active, is_deleted, created_at)
                VALUES
                    ('e1a11111-0000-0000-0000-000000000001', 'Casual Leave', 'CL', 12, true, false, 'Standard annual casual leave for personal work', true, false, NOW()),
                    ('e1a11111-0000-0000-0000-000000000002', 'Sick Leave', 'SL', 10, true, false, 'Medical and sickness leave with certificate verification', true, false, NOW()),
                    ('e1a11111-0000-0000-0000-000000000003', 'Earned Leave', 'EL', 15, true, true, 'Privilege/Earned leaves eligible for carry-forward and encashment', true, false, NOW()),
                    ('e1a11111-0000-0000-0000-000000000004', 'Loss of Pay', 'LOP', 0, false, false, 'Unpaid leave where salary deduction applies automatically', true, false, NOW())
                ON CONFLICT (id) DO NOTHING;

                -- Default departments seed per tenant schema
                INSERT INTO ""{tenantProvider.SchemaName}"".hr_departments (id, name, code, description, head_of_department, is_active, is_deleted, created_at)
                VALUES
                    ('d1a11111-0000-0000-0000-000000000001', 'Sales & Counter Billing', 'SALES', 'Retail counter sales, mobile retail consultants & billing cashiers', 'Store Manager', true, false, NOW()),
                    ('d1a11111-0000-0000-0000-000000000002', 'Inventory & Warehouse', 'INVENTORY', 'Device stocking, godown receiving, barcode allocation & dispatch', 'Warehouse Lead', true, false, NOW()),
                    ('d1a11111-0000-0000-0000-000000000003', 'Accounts & Finance', 'ACCOUNTS', 'GST compliance, tally reconciliation, vendor payments & payroll ledger', 'Chief Accountant', true, false, NOW()),
                    ('d1a11111-0000-0000-0000-000000000004', 'Administration & HR', 'ADMIN', 'Store operations, staff management, onboarding & compliance', 'Operations Manager', true, false, NOW())
                ON CONFLICT (id) DO NOTHING;

                -- Ensure new columns exist on hr_employees for existing tenant schemas
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = '{tenantProvider.SchemaName}' 
                          AND table_name = 'hr_employees'
                    ) THEN
                        ALTER TABLE ""{tenantProvider.SchemaName}"".hr_employees 
                            ADD COLUMN IF NOT EXISTS salary_type character varying(30) DEFAULT 'Monthly',
                            ADD COLUMN IF NOT EXISTS reports_to_employee_id uuid,
                            ADD COLUMN IF NOT EXISTS reports_to_employee_name character varying(150),
                            ADD COLUMN IF NOT EXISTS bank_account_number character varying(50),
                            ADD COLUMN IF NOT EXISTS bank_name character varying(100),
                            ADD COLUMN IF NOT EXISTS bank_ifsc_code character varying(20),
                            ADD COLUMN IF NOT EXISTS pan_number character varying(20),
                            ADD COLUMN IF NOT EXISTS aadhaar_number character varying(30),
                            ADD COLUMN IF NOT EXISTS uan_number character varying(30);
                    END IF;
                END $$;

                -- Master Items Table for HR & Payroll
                CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".hr_master_items (
                    id uuid NOT NULL PRIMARY KEY,
                    category character varying(50) NOT NULL,
                    name character varying(150) NOT NULL,
                    code character varying(50) NOT NULL,
                    description text,
                    sort_order integer NOT NULL DEFAULT 0,
                    is_default boolean NOT NULL DEFAULT false,
                    is_active boolean NOT NULL DEFAULT true,
                    is_deleted boolean NOT NULL DEFAULT false,
                    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                    created_by uuid,
                    modified_at timestamp with time zone,
                    modified_by uuid
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ix_hr_master_items_category_code 
                    ON ""{tenantProvider.SchemaName}"".hr_master_items (category, code) WHERE NOT is_deleted;

                -- Seed standard HR Masters
                INSERT INTO ""{tenantProvider.SchemaName}"".hr_master_items (id, category, name, code, description, sort_order, is_default, is_active, is_deleted, created_at)
                VALUES
                    -- Job Titles
                    ('a1a11111-0000-0000-0000-000000000001', 'JOB_TITLE', 'Store Manager', 'STORE_MGR', 'Overall retail branch & inventory incharge', 1, true, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000002', 'JOB_TITLE', 'Senior Sales Consultant', 'SR_SALES', 'Counter sales & smartphone consultant', 2, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000003', 'JOB_TITLE', 'Billing Cashier', 'CASHIER', 'POS counter billing and POS machine handler', 3, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000004', 'JOB_TITLE', 'Inventory & Stock Incharge', 'STOCK_MGR', 'Barcode verification, godown inward and dispatch', 4, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000005', 'JOB_TITLE', 'Accountant & Tax Executive', 'ACCOUNTANT', 'GST bookkeeping and tally reconciliation', 5, false, true, false, NOW()),
                    -- Employment Types
                    ('a1a11111-0000-0000-0000-000000000010', 'EMPLOYMENT_TYPE', 'Full Time', 'FULL_TIME', 'Permanent monthly payroll staff', 1, true, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000011', 'EMPLOYMENT_TYPE', 'Part Time', 'PART_TIME', 'Hourly / partial shift workforce', 2, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000012', 'EMPLOYMENT_TYPE', 'Contract / Retainer', 'CONTRACT', 'Third-party agency or contracted staff', 3, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000013', 'EMPLOYMENT_TYPE', 'Trainee / Intern', 'INTERN', 'Stipend-based trainee period', 4, false, true, false, NOW()),
                    -- Employee Status
                    ('a1a11111-0000-0000-0000-000000000020', 'EMPLOYEE_STATUS', 'Active', 'ACTIVE', 'Active on payroll and store shifts', 1, true, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000021', 'EMPLOYEE_STATUS', 'Probation', 'PROBATION', 'New recruit under 3-6 months evaluation', 2, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000022', 'EMPLOYEE_STATUS', 'On Leave', 'ON_LEAVE', 'Approved extended or maternity leave', 3, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000023', 'EMPLOYEE_STATUS', 'Resigned', 'RESIGNED', 'Serving notice period', 4, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000024', 'EMPLOYEE_STATUS', 'Terminated', 'TERMINATED', 'Employment officially ended', 5, false, true, false, NOW()),
                    -- Salary Types
                    ('a1a11111-0000-0000-0000-000000000030', 'SALARY_TYPE', 'Monthly Gross Salary', 'Monthly', 'Direct gross salary paid on monthly cycle', 1, true, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000031', 'SALARY_TYPE', 'Annual Cost to Company (CTC)', 'YearlyPackage', 'Total annual cost inclusive of statutory bonuses & PF', 2, false, true, false, NOW()),
                    -- Leave Categories
                    ('a1a11111-0000-0000-0000-000000000040', 'LEAVE_CATEGORY', 'General Paid Leave', 'PAID', 'Eligible for normal salary disbursement', 1, true, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000041', 'LEAVE_CATEGORY', 'Medical / Certified', 'MEDICAL', 'Requires doctor note or certificate', 2, false, true, false, NOW()),
                    ('a1a11111-0000-0000-0000-000000000042', 'LEAVE_CATEGORY', 'Loss of Pay / Unpaid', 'UNPAID', 'Deducted directly from monthly attendance', 3, false, true, false, NOW())
                ON CONFLICT (id) DO NOTHING;

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
