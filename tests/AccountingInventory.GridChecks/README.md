# Grid sorting regression checks

From `MicroservicesEcosystem`, run:

```powershell
dotnet run --project tests/AccountingInventory.GridChecks -- --local
```

`--local` reads the API's appsettings connection string and requires a localhost
PostgreSQL server. Alternatively, set `GRID_CHECK_CONNECTION_STRING` and omit
`--local`.

The checks create a temporary schema and fixture data inside a transaction,
exercise ascending and descending sorting across pages for all 16 grid query
handlers, and check multi-column sorting and invalid-field fallback. The entire
transaction is rolled back, including the temporary schema.

Run frontend checks from `react-multitenant-saas`:

```powershell
node tests/gridSorting.cjs
```

The suite also covers purchase reference integrity, invoice dates and terms,
permissions, payment chronology, financial closing, journal reversal protections,
and legacy contact repair. The two-session concurrency probe creates a separately
committed scratch schema, proves conflict handling and full rollback, and drops
that schema in `finally`. Neither test schema is an existing tenant schema.

Pass `--business-only` alongside `--local` to run the accounting/inventory and
transaction checks without repeating the grid pagination suite.
