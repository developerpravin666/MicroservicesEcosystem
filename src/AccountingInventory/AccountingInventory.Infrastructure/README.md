# To Migrate tables in database
1. Open the terminal and navigate to the project directory. e.g. cd src/AccountingInventory
2. Run the following command to apply migrations:
		dotnet ef migrations add InitialCreate --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context TenantDBContext
		OR
		# To Remove the migration if needed
		dotnet ef migrations remove --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context TenantDBContext
3. update database with the following command:
		dotnet ef database update --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context TenantDBContext

# To Migrate tables in Accounting and Inventory DB Context

The API applies pending migrations to every active tenant schema on startup via
`ApplyTenantSchemaMigrationsAsync`. Rebuild and restart the API after schema changes;
updating only the `public` schema does not update tenant tables.

`20261022120000_RepairExistingAccountingTables` repairs missing invoice accounting
columns in `products` and `sales_products`, and reversal/dimension columns in the
ledger tables. It backfills only missing values. Due dates use the invoice date
plus payment terms, including inserts from older application versions that omit `due_date`.
The database fallback also recalculates due dates when an older writer changes
the invoice date or terms without supplying a new due date.

Run the regression checks against local PostgreSQL from the repository root:

```powershell
dotnet run --project tests/AccountingInventory.SchemaChecks -- --local
```

Alternatively, set `SCHEMA_CHECK_CONNECTION_STRING` and omit `--local`. Checks
reproduce the original constraint error, test legacy inserts/updates, backfills,
repeat execution and retained invoice/tax values. They create a temporary schema
inside a transaction and roll it back; tenant data is not changed.

1. Open the terminal and navigate to the project directory. e.g. cd src/AccountingInventory
2. Run the following command to apply migrations:
		dotnet ef migrations add InitialCreate --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context AccountingInventoryDbContext
		OR
		# To Remove the migration if needed
		dotnet ef migrations remove --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context AccountingInventoryDbContext
3. update database with the following command:
		dotnet ef database update --project AccountingInventory.Infrastructure --startup-project AccountingInventory.API --context AccountingInventoryDbContext


`20261023120000_RepairUserContacts` corrects historical registration rows where
email and mobile were passed to the domain factory in reverse order. It swaps
only pairs with an email-shaped mobile and a phone-shaped email, preserving
ambiguous values for review. It uses the same tenant migration startup path.

See [product readiness](../../../docs/accounting-inventory-readiness.md) for
permission changes, verified safeguards, and remaining release requirements.
