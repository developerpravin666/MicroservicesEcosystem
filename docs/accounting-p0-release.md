# P0 accounting deployment and acceptance

Give business users the [step-by-step user manual](accounting-user-manual.md) or its [printable HTML copy](accounting-user-manual.html). Update both when releasing user-facing workflow changes; see [documentation instructions](README.md).

## Workflows

- Accounting → Returns & Corrections posts a full serialized-unit credit/debit note. Sales return stock to inventory or write it off; purchase returns remove available stock. The exact source journal is reversed and original invoices remain in history. Record customer/supplier refunds separately, limited to paid credit. Corrections are immutable; replacement invoices use the usual purchase/sale workflow.
- Accounting → Opening Balances & Reconciliation imports opening customer/vendor items and inventory cost against accounts 1100/2000/1200 once. Stage opening stock first; it is unavailable for sale until the opening journal commits. Opening settlements post to the same control accounts. The dated reconciliation report exposes differences instead of silently modifying financial history.
- Accounting → Staff Access invites users, enables/disables verified staff, grants permissions, and transfers the persisted owner designation. New owners require an operator-issued invitation via `POST /api/tenants/{id}/invite-owner` with `Tenants.Manage`. New staff accept emailed one-use tokens at `/accept-invitation`. Normal password recovery remains available to active owners/staff. Direct password registration is disabled.
- Invoice snapshots freeze buyer/supplier, product/serial and financial details. New sales also capture seller settings. Existing snapshots are explicitly marked `reconstructed`: older historical names cannot be recovered from mutable master records.

## Upgrade

1. Configure environment/secrets: `ConnectionStrings__AccountingInventoryDb`, `Jwt__Issuer`, `Jwt__Audience`, a unique `Jwt__SigningKey`, HTTPS `Frontend__BaseUrl`, and the `Email__SmtpHost`, `Email__SmtpPort`, `Email__From`, `Email__Username`, `Email__Password`, `Email__EnableSsl` settings. Preserve JWT key access and owner recovery mailbox access in your secret-management procedure. Remote production PostgreSQL requires TLS. The API validates these requirements at startup outside Development.
2. Configure PostgreSQL client authentication through `PGPASSFILE`/`pg_service.conf`; restrict backup-directory permissions and encrypt/copy archives to your managed backup store. Run `ops/Backup-Accounting.ps1 -Database <database> -Destination <backup-directory>` and `ops/Test-AccountingRestore.ps1 -Archive <archive>` on an isolated restore host with matching PostgreSQL tools. The restore script creates and retains a new scratch database; it never overwrites production.
3. Rehearse API migrations against that restored database with production email delivery disabled/routed to a test mailbox. Check invoice/snapshot counts, owner designation and GL control differences. Existing owner selection is preserved once from the earliest non-deleted user. Existing accounts are grandfathered; invitations enforce verification for new accounts.
4. Build/restart the API and deploy the frontend together. Pending migrations run per active tenant at startup; suspended tenants migrate when reactivated. Do not deploy the frontend against the old API. Grant existing staff permissions before they resume writes.
5. Monitor `/health` for liveness and `/health/ready` for database/registry readiness. Alert on readiness failures, repeated 409 transaction conflicts, failed invitations, and nonzero GL reconciliation differences; retain structured request/correlation logs in your monitoring platform.

## Test email delivery using the API

With the API running, use Swagger or an HTTP client to call `POST /api/email/test`. Supply an `Authorization: Bearer <access-token>` header for a signed-in administrator with `Tenants.Manage`, and `Content-Type: application/json`. No tenant header is required. Request body:

```json
{ "to": "recipient@example.com" }
```

The endpoint uses the existing SMTP sender and API configuration, including environment overrides. It sends a fixed subject (`Siddhi Mobile test email`) and HTML message to one validated recipient. Invalid addresses return HTTP 400; missing authentication returns 401 and missing permission returns 403.

HTTP 200 with `sent: true` and `status: "smtpAccepted"` means SMTP accepted the message; check the recipient inbox/spam folder to verify delivery. HTTP 200 with `sent: false` and `status: "offline"` means no email was sent. SMTP/configuration failures return HTTP 502 with a generic error; consult server logs for details. Existing invitation/password email fallback behavior is preserved.

The checked-in settings use offline mode. Real sending requires both `Email__OfflineMode` and `Deployment__OfflineMode` to be false, a real SMTP host, and valid sender/TLS/credential settings. Restart the API after changing configuration supplied through environment variables.

For SMTP submission on port 587, set `Email__EnableSsl=true`. The existing .NET SMTP client uses this setting to issue STARTTLS. A `5.7.0 Must issue a STARTTLS command first` response indicates that TLS is disabled in the effective configuration; check environment overrides as well as `Email:EnableSsl` in appsettings.json, restart the API, and retry the test endpoint.

## Recovery

Stop writes during a deployment incident. Capture a fresh backup for investigation. Before new correction/opening data exists, schema downgrade can be rehearsed on the restore copy. After these workflows are used, the migration explicitly rejects dropping financial history; recover using the rehearsed pre-upgrade backup and compatible application build. Review and re-enter intervening transactions under accountant supervision. Set and verify your retention, RPO/RTO and backup schedule in the deployment platform.

## Required evidence before production sign-off

- Run both repository database check suites against a local/scratch database, build the API/frontend, and run the API isolation checks. Fixtures must never point at production.
- Walk through purchase → payment → sale → receipt → customer return → refund → resale and supplier return → refund. Verify stock, balanced journals, invoice snapshots, GST totals, control-account reconciliation and permission denial for ungranted staff.
- Test opening-stock staging, matched/mismatched control totals, opening settlements, replayed/expired invitation tokens, ownership transfer and mailbox password recovery.
- Record a successful restore and upgrade rehearsal using your deployment backup/credentials; capture monitoring and alert evidence.
- Obtain accountant review of opening balances, historical differences and credit/debit-note treatment, then business acceptance of the workflows. These approvals cannot be substituted by automated code checks.

Full-unit returns are implemented. Whole multi-line invoice returns are also implemented. Partial invoice returns, partial price adjustments and statutory reporting expansion remain separately scoped work.

Operational postings and opening settlements must be dated after the cutover date. Opening stock is excluded from new purchase bills and input-tax reports; its historical balances belong in the cutover journal. Use View note to print credit/debit notes with frozen invoice details, tax reversals and recorded refund history.

## Mandatory invoice integrity upgrade

Deploy migration `20261026120000_MandatoryInvoiceIntegrity` with this API and frontend. It adds separate multi-line invoice receipts, usable/refundable customer advances, persisted request replay records, HSN/SAC and unit snapshots, four-decimal quantities and IGST correction amounts. Legacy single-product receipt foreign keys remain intact. Existing CustomerAdvance journals whose source identifies the customer are backfilled to advance records and linked to their original journal; rehearse this conversion on the restored database and reconcile the resulting customer credit.

The downgrade rejects removing used receipts, advances, request records, IGST notes or invoice particulars that would lose financial history. Do not bypass this guard. No live tenant migration is performed as part of source-code validation.

Acceptance must include multi-line mixed-rate and interstate invoices, duplicate serialized-product rejection, fractional quantities, customer allocation overpayments, advance application/refund, full invoice return, historical statement/aging, GST rate-bucket reversal and lost-response replay. The grid checks cover ledger/subledger reconciliation, authorization and actual committed invoice/receipt replay; browser checks cover frozen invoice printing and retained retry keys. These automated checks complement the restore rehearsal and business acceptance above.

## Accessory inventory and integrity release

Deploy migration `20261027120000_AccessorySkuInventory` after MandatoryInvoiceIntegrity, together with the updated API and frontend. It adds SKU balances, immutable quantity/value movements and optional invoice SKU references. Existing standard invoice lines remain historical untracked lines; new accessory lines must reference a SKU. Do not reconstruct stock from old accessory invoice descriptions: count and reconcile actual stock before staging opening quantities or recording a documented acquisition.

Rehearse SKU creation, opening staging/cutover, supplier receipts and payments, mixed purchase bills, weighted-average sales, exact-cost customer returns, whole supplier receipt returns, refunds, write-offs and negative/backdated stock rejection. Compare stock, supplier and tax reports to the ledger. Downgrade refuses to remove used accessory movement history. Ordinary manual control-account postings are now rejected; historical control adjustments require approved FinancialCorrection journals.

## Automated tenant onboarding & control-plane schema release

Deploy migration `20261009120000_AddTenantRegistrationFields` for `TenantDbContext` (control-plane schema `tenant`). It adds the owner contact, GSTIN, store address, initial password hash and approval rejection reason columns to `tenant.tenants`. This migration is automatically applied at application boot via `TenantDbContext.Database.MigrateAsync()`, with idempotent fallback statements in `Program.cs`.

Source validation does not certify the deployment environment. Production sign-off still requires the backup/restore and migration rehearsal, HTTPS/secrets/email configuration, permissions review, monitoring and accountant acceptance described above. No live database migration or deployment is performed by this task.

## Master data import & staging schema release

Deploy migration `20261028120000_MasterDataImportStaging` for `AccountingInventoryDbContext` (tenant schemas). It provisions `master_import_batches` and `master_import_staging_rows` with foreign keys, indexes, and status tracking to support safe two-phase excel master data import (staging -> validation/review -> final database commit). This migration is automatically applied across all active tenant schemas at boot via `TenantSchemaMigrator.MigrateAsync` and `ApplyTenantSchemaMigrationsAsync()`.

