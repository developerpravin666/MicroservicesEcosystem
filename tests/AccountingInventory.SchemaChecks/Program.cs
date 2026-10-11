using System.Text.Json;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;
using Npgsql;

// No changes to tenant schemas: all SQL runs in a temporary schema inside a
// transaction that is rolled back, including on assertion failure.
var connectionString = Environment.GetEnvironmentVariable("SCHEMA_CHECK_CONNECTION_STRING");
if (args.Contains("--local"))
{
    var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repository,
        "src/AccountingInventory/AccountingInventory.Api/appsettings.json")));
    connectionString = settings.RootElement.GetProperty("ConnectionStrings")
        .GetProperty("AccountingInventoryDb").GetString();
    var localSettings = new NpgsqlConnectionStringBuilder(connectionString);
    if (localSettings.Host is not ("localhost" or "127.0.0.1" or "::1"))
        throw new InvalidOperationException("--local requires a local PostgreSQL host.");
}
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set SCHEMA_CHECK_CONNECTION_STRING or use --local for the local appsettings database.");

var reference = Guid.NewGuid();
var invoiceDate = new DateOnly(2026, 9, 1);
var product = Product.Create(reference, reference, reference, reference, reference, reference,
    "schema-check", null, "test", 100, 10, 9, 9, 18, invoiceDate, 30);
Check(product.DueDate == invoiceDate.AddDays(30), "Domain creates purchase due date from invoice terms");
product.Update(reference, reference, reference, reference, reference, reference,
    "schema-check", null, "test", 100, 10, 9, 9, 18, invoiceDate, 45);
Check(product.DueDate == invoiceDate.AddDays(45), "Domain updates purchase due date from invoice terms");

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
var schema = "schema_check_" + Guid.NewGuid().ToString("N");
await Execute($"CREATE SCHEMA {schema}; SET LOCAL search_path TO {schema};");
try
{
    await Execute("""
        CREATE TABLE products (
            id integer PRIMARY KEY, created_at timestamptz NOT NULL,
            purchase_date date NULL, payment_terms_days integer NULL, due_date date NOT NULL,
            purchase_price numeric(18,2) NOT NULL DEFAULT 100, discount numeric(18,2) NOT NULL DEFAULT 10,
            cgst numeric(5,2) NOT NULL DEFAULT 9, sgst numeric(5,2) NOT NULL DEFAULT 9
        );
        CREATE TABLE sales_products (
            id integer PRIMARY KEY, sale_date date NOT NULL,
            selling_price numeric(18,2) NOT NULL DEFAULT 100, discount numeric(18,2) NOT NULL DEFAULT 10
        );
        CREATE TABLE taxes (id uuid PRIMARY KEY);
        CREATE TABLE accounting_dimensions (id uuid PRIMARY KEY);
        INSERT INTO products (id, created_at, purchase_date, payment_terms_days, due_date)
            VALUES (1, '2026-09-01Z', '2026-09-01', 30, '2026-10-01');
        SAVEPOINT legacy_insert;
        """);
    try
    {
        await Execute("INSERT INTO products (id, created_at, purchase_date) VALUES (2, '2026-09-01Z', '2026-09-01');");
        throw new Exception("Expected the original NOT NULL due_date failure.");
    }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.NotNullViolation)
    {
        Check(exception.ColumnName == "due_date", "Reproduced original due_date constraint failure");
        await Execute("ROLLBACK TO SAVEPOINT legacy_insert;");
    }
    await Execute("""
        ALTER TABLE products ALTER COLUMN due_date DROP NOT NULL;
        INSERT INTO products (id, created_at, payment_terms_days) VALUES (2, '2026-09-02Z', 15);
        INSERT INTO sales_products (id, sale_date) VALUES (1, '2026-09-01');
        """);
    await Repair();
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 1") == new DateOnly(2026, 10, 1), "Preserves existing due dates");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 2") == new DateOnly(2026, 9, 17), "Backfills date using historical invoice date and terms");
    Check(await Scalar<decimal>("SELECT total_amount FROM products WHERE id = 2") == 106.20m, "Backfills discounted purchase totals");
    Check(await Scalar<decimal>("SELECT total_amount FROM sales_products WHERE id = 1") == 90m, "Backfills missing sales tax totals");

    await Execute("INSERT INTO products (id, created_at, purchase_date, payment_terms_days, total_amount) VALUES (3, '2026-09-01Z', '2026-09-01', 30, 106.2);");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 3") == new DateOnly(2026, 10, 1), "Legacy purchase insert omitting due_date succeeds");
    await Execute("INSERT INTO products (id, created_at, purchase_date, payment_terms_days, due_date, total_amount) VALUES (4, '2026-09-01Z', '2026-09-01', NULL, NULL, 106.2);");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 4") == invoiceDate, "Explicit null due date and terms use invoice date and zero terms");
    await Execute("UPDATE products SET payment_terms_days = 45 WHERE id = 3;");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 3") == invoiceDate.AddDays(45), "Legacy update recalculates due date when terms change");
    await Execute("UPDATE products SET purchase_date = '2026-09-02' WHERE id = 3;");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 3") == new DateOnly(2026, 9, 2).AddDays(45), "Legacy update recalculates due date when invoice date changes");
    await Execute("UPDATE products SET payment_terms_days = 60, due_date = '2026-12-01' WHERE id = 3;");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 3") == new DateOnly(2026, 12, 1), "Preserves an explicitly supplied due date");
    await Execute("INSERT INTO sales_products (id, sale_date, payment_terms_days) VALUES (2, '2026-09-01', 30);");
    Check(await Scalar<DateOnly>("SELECT due_date FROM sales_products WHERE id = 2") == invoiceDate.AddDays(30), "Legacy sales insert omitting due_date succeeds");
    await Execute("UPDATE sales_products SET sale_date = '2026-09-02', payment_terms_days = 45 WHERE id = 2;");
    Check(await Scalar<DateOnly>("SELECT due_date FROM sales_products WHERE id = 2") == new DateOnly(2026, 9, 2).AddDays(45), "Legacy sales update recalculates due date");

    // Simulate ledger tables repaired after later migrations were recorded.
    Check(await Scalar<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'journal_entries' AND column_name = 'reversal_of_journal_entry_id')"), "Restores ledger reversal column");
    Check(await Scalar<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'journal_lines' AND column_name = 'dimension_id')"), "Restores ledger dimension column");
    Check(await Scalar<long>("SELECT count(*) FROM pg_constraint WHERE connamespace = current_schema()::regnamespace AND contype = 'f'") == 5, "Restores all ledger and sales tax foreign keys");
    await Execute("UPDATE sales_products SET cgst_rate = 9, sgst_rate = 9, cgst_amount = 8.1, sgst_amount = 8.1, total_amount = 106.2 WHERE id = 1;");
    await Repair();
    Check(await Scalar<decimal>("SELECT total_amount FROM sales_products WHERE id = 1") == 106.2m, "Repeated repair preserves existing tax amounts");
    Check(await Scalar<DateOnly>("SELECT due_date FROM products WHERE id = 3") == new DateOnly(2026, 12, 1), "Repeated repair preserves explicit due dates");
    Console.WriteLine("All accounting schema checks passed.");
}
finally
{
    await transaction.RollbackAsync();
}

async Task Repair()
{
    await Execute(JournalLedgerTableRepair.Sql);
    await Execute(ExistingAccountingTableRepair.Sql);
}
async Task Execute(string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    await command.ExecuteNonQueryAsync();
}
async Task<T> Scalar<T>(string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction);
    return (T)(await command.ExecuteScalarAsync() ?? throw new Exception("Expected query result."));
}
static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
}
