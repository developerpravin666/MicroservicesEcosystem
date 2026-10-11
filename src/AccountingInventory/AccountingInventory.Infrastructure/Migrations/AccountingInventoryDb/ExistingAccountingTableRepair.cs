namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

internal static class ExistingAccountingTableRepair
{
    // Repair physical columns even if an earlier migration is already recorded.
    // Only missing values are backfilled; existing invoice/tax values are retained.
    // All objects resolve in the current tenant's search path.
    public const string Sql = """
        ALTER TABLE products ADD COLUMN IF NOT EXISTS purchase_date date NULL;
        ALTER TABLE products ADD COLUMN IF NOT EXISTS payment_terms_days integer NULL;
        ALTER TABLE products ADD COLUMN IF NOT EXISTS due_date date NULL;
        ALTER TABLE products ADD COLUMN IF NOT EXISTS total_amount numeric(18,2) NULL;
        UPDATE products SET purchase_date = COALESCE(created_at::date, CURRENT_DATE) WHERE purchase_date IS NULL;
        UPDATE products SET payment_terms_days = 0 WHERE payment_terms_days IS NULL;
        UPDATE products SET due_date = purchase_date + payment_terms_days WHERE due_date IS NULL;
        UPDATE products SET total_amount = ROUND((purchase_price - discount) * (1 + (cgst + sgst) / 100), 2)
            WHERE total_amount IS NULL;
        ALTER TABLE products ALTER COLUMN purchase_date SET DEFAULT CURRENT_DATE;
        ALTER TABLE products ALTER COLUMN purchase_date SET NOT NULL;
        ALTER TABLE products ALTER COLUMN payment_terms_days SET DEFAULT 0;
        ALTER TABLE products ALTER COLUMN payment_terms_days SET NOT NULL;
        ALTER TABLE products ALTER COLUMN due_date SET NOT NULL;
        ALTER TABLE products ALTER COLUMN total_amount SET NOT NULL;

        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS payment_terms_days integer NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS due_date date NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS cgst_rate numeric(5,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS sgst_rate numeric(5,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS taxable_amount numeric(18,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS cgst_amount numeric(18,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS sgst_amount numeric(18,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS total_amount numeric(18,2) NULL;
        ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS tax_id uuid NULL;
        UPDATE sales_products SET payment_terms_days = 0 WHERE payment_terms_days IS NULL;
        UPDATE sales_products SET due_date = sale_date + payment_terms_days WHERE due_date IS NULL;
        UPDATE sales_products SET cgst_rate = 0 WHERE cgst_rate IS NULL;
        UPDATE sales_products SET sgst_rate = 0 WHERE sgst_rate IS NULL;
        UPDATE sales_products SET taxable_amount = GREATEST(selling_price - discount, 0) WHERE taxable_amount IS NULL;
        UPDATE sales_products SET cgst_amount = ROUND(taxable_amount * cgst_rate / 100, 2) WHERE cgst_amount IS NULL;
        UPDATE sales_products SET sgst_amount = ROUND(taxable_amount * sgst_rate / 100, 2) WHERE sgst_amount IS NULL;
        UPDATE sales_products SET total_amount = taxable_amount + cgst_amount + sgst_amount WHERE total_amount IS NULL;
        ALTER TABLE sales_products ALTER COLUMN payment_terms_days SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN payment_terms_days SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN due_date SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN cgst_rate SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN cgst_rate SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN sgst_rate SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN sgst_rate SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN taxable_amount SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN taxable_amount SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN cgst_amount SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN cgst_amount SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN sgst_amount SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN sgst_amount SET NOT NULL;
        ALTER TABLE sales_products ALTER COLUMN total_amount SET DEFAULT 0;
        ALTER TABLE sales_products ALTER COLUMN total_amount SET NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_sales_products_tax_id ON sales_products (tax_id);

        -- A constant default cannot calculate a due date from another column.
        -- This fallback supports older writers that omit the new due_date column,
        -- while the current application continues to send its calculated value.
        ALTER TABLE products ALTER COLUMN due_date DROP DEFAULT;
        ALTER TABLE sales_products ALTER COLUMN due_date DROP DEFAULT;
        CREATE OR REPLACE FUNCTION set_invoice_due_date() RETURNS trigger
        LANGUAGE plpgsql AS $function$
        DECLARE
            invoice_date date;
            recalculate boolean;
        BEGIN
            NEW.payment_terms_days := COALESCE(NEW.payment_terms_days, 0);
            invoice_date := (to_jsonb(NEW) ->> TG_ARGV[0])::date;
            recalculate := NEW.due_date IS NULL;
            IF TG_OP = 'UPDATE' THEN
                recalculate := recalculate OR (
                    NEW.due_date IS NOT DISTINCT FROM OLD.due_date AND (
                        (to_jsonb(NEW) ->> TG_ARGV[0]) IS DISTINCT FROM (to_jsonb(OLD) ->> TG_ARGV[0])
                        OR NEW.payment_terms_days IS DISTINCT FROM OLD.payment_terms_days
                    )
                );
            END IF;
            IF recalculate THEN
                NEW.due_date := invoice_date + NEW.payment_terms_days;
            END IF;
            RETURN NEW;
        END;
        $function$;
        DROP TRIGGER IF EXISTS products_invoice_due_date ON products;
        CREATE TRIGGER products_invoice_due_date
            BEFORE INSERT OR UPDATE OF purchase_date, payment_terms_days, due_date ON products
            FOR EACH ROW EXECUTE FUNCTION set_invoice_due_date('purchase_date');
        DROP TRIGGER IF EXISTS sales_products_invoice_due_date ON sales_products;
        CREATE TRIGGER sales_products_invoice_due_date
            BEFORE INSERT OR UPDATE OF sale_date, payment_terms_days, due_date ON sales_products
            FOR EACH ROW EXECUTE FUNCTION set_invoice_due_date('sale_date');

        -- The earlier ledger repair recreated the base tables without these
        -- later accounting extensions. Restore columns, indexes and relationships.
        ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS reversal_of_journal_entry_id uuid NULL;
        ALTER TABLE journal_lines ADD COLUMN IF NOT EXISTS dimension_id uuid NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_reversal_of_journal_entry_id
            ON journal_entries (reversal_of_journal_entry_id) WHERE reversal_of_journal_entry_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_journal_lines_dimension_id ON journal_lines (dimension_id);
        DO $repair$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conname = 'fk_sales_products_taxes_tax_id' AND conrelid = 'sales_products'::regclass) THEN
                ALTER TABLE sales_products ADD CONSTRAINT fk_sales_products_taxes_tax_id
                    FOREIGN KEY (tax_id) REFERENCES taxes (id) ON DELETE RESTRICT;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conname = 'fk_journal_entries_journal_entries_reversal_of_journal_entry_id'
                    AND conrelid = 'journal_entries'::regclass) THEN
                ALTER TABLE journal_entries ADD CONSTRAINT fk_journal_entries_journal_entries_reversal_of_journal_entry_id
                    FOREIGN KEY (reversal_of_journal_entry_id) REFERENCES journal_entries (id) ON DELETE RESTRICT;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint
                WHERE conname = 'fk_journal_lines_accounting_dimensions_dimension_id'
                    AND conrelid = 'journal_lines'::regclass) THEN
                ALTER TABLE journal_lines ADD CONSTRAINT fk_journal_lines_accounting_dimensions_dimension_id
                    FOREIGN KEY (dimension_id) REFERENCES accounting_dimensions (id) ON DELETE RESTRICT;
            END IF;
        END;
        $repair$;
        """;
}
