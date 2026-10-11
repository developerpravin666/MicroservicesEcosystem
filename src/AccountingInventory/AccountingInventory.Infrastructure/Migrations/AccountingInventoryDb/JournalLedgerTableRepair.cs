namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

internal static class JournalLedgerTableRepair
{
    // Idempotent so the repair works for both fresh schemas and schemas where
    // migration history is ahead of the physical tables. Existing tables and
    // rows are left untouched.
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS chart_accounts (
            id uuid NOT NULL,
            code character varying(20) NOT NULL,
            name character varying(150) NOT NULL,
            type character varying(20) NOT NULL,
            normal_balance character varying(10) NOT NULL,
            is_system boolean NOT NULL,
            created_at timestamp with time zone NOT NULL,
            created_by uuid NULL,
            modified_at timestamp with time zone NULL,
            modified_by uuid NULL,
            is_active boolean NOT NULL,
            is_deleted boolean NOT NULL,
            CONSTRAINT pk_chart_accounts PRIMARY KEY (id)
        );

        CREATE TABLE IF NOT EXISTS journal_entries (
            id uuid NOT NULL,
            journal_number character varying(50) NOT NULL,
            journal_date date NOT NULL,
            description character varying(500) NOT NULL,
            source_type character varying(60) NULL,
            source_id character varying(128) NULL,
            posted_at timestamp with time zone NOT NULL,
            created_at timestamp with time zone NOT NULL,
            created_by uuid NULL,
            modified_at timestamp with time zone NULL,
            modified_by uuid NULL,
            is_active boolean NOT NULL,
            is_deleted boolean NOT NULL,
            CONSTRAINT pk_journal_entries PRIMARY KEY (id)
        );

        CREATE TABLE IF NOT EXISTS journal_lines (
            id uuid NOT NULL,
            journal_entry_id uuid NOT NULL,
            account_id uuid NOT NULL,
            debit numeric(18,2) NOT NULL,
            credit numeric(18,2) NOT NULL,
            memo character varying(250) NULL,
            created_at timestamp with time zone NOT NULL,
            created_by uuid NULL,
            modified_at timestamp with time zone NULL,
            modified_by uuid NULL,
            is_active boolean NOT NULL,
            is_deleted boolean NOT NULL,
            CONSTRAINT pk_journal_lines PRIMARY KEY (id),
            CONSTRAINT fk_journal_lines_chart_accounts_account_id
                FOREIGN KEY (account_id) REFERENCES chart_accounts (id) ON DELETE RESTRICT,
            CONSTRAINT fk_journal_lines_journal_entries_journal_entry_id
                FOREIGN KEY (journal_entry_id) REFERENCES journal_entries (id) ON DELETE RESTRICT
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_chart_accounts_code ON chart_accounts (code);
        CREATE INDEX IF NOT EXISTS ix_journal_entries_journal_date ON journal_entries (journal_date);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_journal_number ON journal_entries (journal_number);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_source_type_source_id
            ON journal_entries (source_type, source_id)
            WHERE source_type IS NOT NULL AND source_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_journal_lines_account_id ON journal_lines (account_id);
        CREATE INDEX IF NOT EXISTS ix_journal_lines_journal_entry_id ON journal_lines (journal_entry_id);
        """;
}
