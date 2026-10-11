# Accounting and inventory product readiness

This review targets the existing INR, CGST/SGST, serialized-product application.
It does not certify a complete accounting product or statutory compliance.

## Implemented safeguards

- Tenant business endpoints require a token identifying the same tenant as the header,
  an active registry record, and an active user in that tenant. Refresh and password
  recovery check current tenant/user status instead of trusting stale token state.
- Tenant listing, suspension and reactivation require the operator `Tenants.Manage`
  permission. Ordinary tenant registration/login does not issue that permission.
  Configure a separately secured operator identity before using those operations.
- Core commands run in a serializable database transaction. Stock/balance checks,
  source writes, journals and audit entries commit together. Concurrent conflicts
  return HTTP 409 with no partial writes; the client must refresh before retrying.
  Login, refresh and control-plane provisioning are excluded because they switch schemas.
- Staff require `catalog.manage`, `purchases.manage`, `sales.manage`, `inventory.manage`
  or `accounting.manage` for the corresponding writes. Existing specialized permissions
  remain required for approvals, dimensions, documents, assets and budgets. The earliest
  tenant user's ownership is persisted by migration and can subsequently be transferred
  to a verified user through Staff Access. Existing staff must receive grants before continuing these writes.
- Direct password registration is disabled. Operators invite the initial owner; owners invite staff.
  Email activation tokens are hashed, expire after 24 hours and are consumed once. Staff can be
  enabled/disabled and owner status is explicit rather than recalculated from registration order.
- Purchases validate active references within the tenant and the model's brand/type.
  Invoice units share dates and terms; case variants of bill numbers form one invoice; paid bills cannot gain or change units.
  Serial collision checks are case insensitive across both serial fields.
- Sold, written-off, paid or ledger-posted products cannot be edited. Referenced
  catalog records cannot be deleted, including references in archived source rows.
  Models used by inventory cannot change their brand/type association.
- Sales use recorded inventory cost and invoice year for numbering. Sales cannot
  precede purchases, and payments/receipts cannot precede invoices. Invoice price/discount and receipt precision
  are validated before posting. Sales sorting uses the tax-inclusive invoice total.
- Manual journals cannot impersonate automatic sources. Manual reversals preserve
  dimensions, cannot precede the original entry, and cannot be duplicated. Automatic
  source journals cannot be reversed independently of their operational records.
- Year-end closing cannot run twice. The balance sheet includes nominal closing
  lines, preventing closed earnings from being added twice; operating P&L excludes them.
- Tenant migrations repair invoice/ledger columns and unambiguously swapped user contacts.
- Full serialized-unit sales/purchase returns and cancellations produce linked credit/debit notes,
  exact source-journal reversals, GST reversals and stock disposition atomically. Separate refunds
  are limited to paid credit. Resale follows restocking; returned invoices remain in history.
- Customer/vendor/product invoice snapshots preserve historical display and financial details.
  Existing invoices are backfilled with explicitly reconstructed snapshots. New sale prints use frozen seller settings.
- Opening customer/vendor items and staged stock must reconcile against GL controls before one-time cutover posting.
  Staged stock is unavailable until cutover commits; settlements, dated reconciliation, tax, ageing and stock movement reports include the new workflows.
- Production startup validates secret/TLS/email settings and exposes database readiness. Backup/checksum/restore scripts,
  migration checks, real HTTP/JWT tenant-isolation tests and browser UI tests are available.
  See [P0 release and recovery instructions](accounting-p0-release.md).

## Remaining release requirements

These are substantive missing or incomplete workflows, not features delivered by this patch.

| Area | Required follow-up |
| --- | --- |
| Returns and corrections | Partial price adjustments and more general return quantities beyond full serialized units; accountant acceptance of the implemented full-unit return and cancellation workflows. |
| Invoice model | Multi-line invoices and tenant fiscal-year numbering settings. Current sales represent individual serialized units; immutable snapshots are implemented, and reconstructed historical snapshots require review. |
| Onboarding and staff | Configure the operator identity, production SMTP and owner recovery mailbox; configurable read permissions and any wider public self-service onboarding flow remain separately scoped. Initial owner activation currently requires an operator-issued invitation. |
| Tax scope | Confirm the jurisdiction and required tax types, business/product identifiers, export formats and external reporting integrations. Current CGST/SGST reports alone are not a full tax product. |
| Inventory scope | Warehouses/locations, transfers, counts and count reconciliation, units of measure, reorder thresholds and nonserialized quantities where required. |
| Subledger reconciliation | Vendor/customer statements, advances, cross-invoice payment allocation and bank import deduplication. Opening reconciliation and linked return refunds are implemented. |
| Operations | Apply production secrets, backup schedules, retention/encryption, monitoring/alerts and recovery RPO/RTO in the deployment platform. Record a production-like restore/upgrade rehearsal; tenant export/offboarding and capacity testing remain. |
| Validation | Run acceptance against the deployed API and actual SMTP/backup infrastructure. Automated scratch database/API checks and mocked browser tests are available; accountant review and business acceptance remain external release gates. |

## Verification and deployment

Run from `MicroservicesEcosystem`:

```powershell
dotnet run --project tests/AccountingInventory.GridChecks -m:1 -- --local
dotnet run --project tests/AccountingInventory.SchemaChecks -- --local
dotnet build src/AccountingInventory/AccountingInventory.Api --no-restore -m:1
```

The grid/integrity suite uses rolled-back temporary fixtures; its concurrency probe
uses a separate scratch schema removed after testing. Production tenant data is not
used as test fixtures. If a running API locks DLLs, use a separate `OutDir` for build verification.

Run `npm run build`, `node tests/gridSorting.cjs` and `npm run test:accounting-ui` from `react-multitenant-saas`.
Browser checks use headless Microsoft Edge and mocked API fixtures; API checks use real JWTs and disposable schemas.
Rebuild and restart the API to apply pending migrations to active tenant schemas.
Suspended tenants receive pending migrations when reactivated through provisioning.
Take a backup and rehearse the upgrade on a database copy before production deployment.
Review ambiguous historic user contacts and invoice/subledger differences rather than
rewriting financial history automatically. The changed authorization requires staff
permission grants and an operator identity for tenant administration.
