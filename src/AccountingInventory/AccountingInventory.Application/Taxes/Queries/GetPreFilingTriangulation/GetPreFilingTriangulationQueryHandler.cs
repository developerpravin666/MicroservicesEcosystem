using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Taxes.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetPreFilingTriangulation;

public sealed class GetPreFilingTriangulationQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPreFilingTriangulationQuery, PreFilingTriangulationResultDto>
{
    public async Task<PreFilingTriangulationResultDto> Handle(GetPreFilingTriangulationQuery request, CancellationToken ct)
    {
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.FromDate ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) throw new ConflictException("From date must be on or before to date.");

        var settings = await db.CustomerBillSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == CustomerBillSettings.TenantSettingsId, ct);

        var tenantGstin = settings?.TaxRegistrationNumber ?? "27AABCU9603R1ZM";
        var supplierStateCode = GstReturnHelper.ResolveStateCode(tenantGstin, settings?.StateCode, settings?.StateName);

        var exceptions = new List<TriangulationExceptionDto>();
        var totalVouchersScanned = 0;

        // 1. Scan Sales Invoices
        var salesInvoices = await db.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync(ct);

        var customerIds = salesInvoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        totalVouchersScanned += salesInvoices.Count;

        foreach (var inv in salesInvoices.Where(i => !i.IsCancelled))
        {
            customers.TryGetValue(inv.CustomerId, out var cust);
            var custGstin = !string.IsNullOrWhiteSpace(inv.CustomerGstin) ? inv.CustomerGstin : cust?.Gstin;
            var custName = cust?.Name ?? "Customer";
            var posState = inv.PlaceOfSupplyStateCode ?? cust?.StateCode;
            var isInterState = !string.IsNullOrWhiteSpace(posState) &&
                               !string.Equals(posState, supplierStateCode, StringComparison.OrdinalIgnoreCase);

            // A. Check GSTIN Validity & Mod 36 Checksum if present
            if (!string.IsNullOrWhiteSpace(custGstin))
            {
                if (!GstReturnHelper.IsValidGstin(custGstin))
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Critical",
                        "InvalidGSTIN",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        inv.TotalAmount,
                        $"Customer GSTIN '{custGstin}' does not match standard 15-character format.",
                        "Verify and correct the customer GSTIN before filing GSTR-1."
                    ));
                }
                else if (!GstReturnHelper.CheckGstinChecksum(custGstin))
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Critical",
                        "InvalidGSTIN",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        inv.TotalAmount,
                        $"Customer GSTIN '{custGstin}' fails official Modulo 36 checksum verification.",
                        "Update the customer master with the valid portal GSTIN."
                    ));
                }
            }

            // B. Check Missing State Code / POS
            if (string.IsNullOrWhiteSpace(posState))
            {
                exceptions.Add(new TriangulationExceptionDto(
                    "Critical",
                    "MissingStateCode",
                    "SalesInvoice",
                    inv.BillNumber,
                    inv.InvoiceDate,
                    custName,
                    custGstin,
                    inv.TotalAmount,
                    "Place of Supply (POS) state code is missing or unresolved.",
                    "Assign a valid 2-digit Indian State Code to the invoice or customer profile."
                ));
            }

            // C. Line Items Check (Missing HSN/SAC, Tax Rate Mismatches, Intra/Inter tax rules)
            foreach (var line in inv.Lines)
            {
                // Missing HSN/SAC
                if (string.IsNullOrWhiteSpace(line.HsnSac))
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Warning",
                        "MissingHSN",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        line.TaxableAmount,
                        $"Line #{line.LineNumber} '{line.ItemDescription}' is missing HSN/SAC code.",
                        "Add a 4 or 6-digit HSN/SAC code to avoid portal Table 12 rejection."
                    ));
                }

                // Inter-state tax structure check: Inter-State requires IGST; Intra-State requires CGST+SGST
                if (isInterState && (line.CgstAmount > 0 || line.SgstAmount > 0) && line.IgstAmount == 0)
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Critical",
                        "InterStateTaxMismatch",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        line.TaxableAmount,
                        $"Inter-state invoice to state '{posState}' is charged CGST/SGST instead of IGST on Line #{line.LineNumber}.",
                        "Reclassify transaction taxes to IGST."
                    ));
                }
                else if (!isInterState && line.IgstAmount > 0 && (line.CgstAmount == 0 && line.SgstAmount == 0))
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Critical",
                        "InterStateTaxMismatch",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        line.TaxableAmount,
                        $"Intra-state invoice in home state '{supplierStateCode}' is charged IGST instead of CGST + SGST on Line #{line.LineNumber}.",
                        "Split IGST into equal CGST and SGST proportions."
                    ));
                }

                // CGST and SGST must always be equal in intra-state supplies
                if (!isInterState && Math.Abs(line.CgstAmount - line.SgstAmount) > 0.05m)
                {
                    exceptions.Add(new TriangulationExceptionDto(
                        "Critical",
                        "TaxMismatch",
                        "SalesInvoice",
                        inv.BillNumber,
                        inv.InvoiceDate,
                        custName,
                        custGstin,
                        line.TaxableAmount,
                        $"CGST (₹{line.CgstAmount:F2}) and SGST (₹{line.SgstAmount:F2}) amounts do not match on Line #{line.LineNumber}.",
                        "Correct CGST and SGST tax amounts to equal split."
                    ));
                }
            }
        }

        // 2. Scan Purchase Bills (Products & Vendors)
        var vendors = await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, ct);
        var products = await db.Products.AsNoTracking()
            .Where(p => !p.IsOpeningStock && p.PurchaseDate >= from && p.PurchaseDate <= to)
            .ToListAsync(ct);

        totalVouchersScanned += products.Select(p => p.BillNumber).Distinct().Count();

        foreach (var prod in products)
        {
            vendors.TryGetValue(prod.VendorId, out var vendor);
            var vendorGstin = vendor?.GstNumber;

            if (!string.IsNullOrWhiteSpace(vendorGstin) && !GstReturnHelper.IsValidGstin(vendorGstin))
            {
                exceptions.Add(new TriangulationExceptionDto(
                    "Warning",
                    "InvalidGSTIN",
                    "PurchaseBill",
                    prod.BillNumber ?? "UNSPECIFIED",
                    prod.PurchaseDate,
                    vendor?.Name,
                    vendorGstin,
                    prod.TotalAmount,
                    $"Supplier '{vendor?.Name}' has invalid GSTIN format '{vendorGstin}'.",
                    "Update vendor master with registered GSTIN to avoid ITC denial in GSTR-2B."
                ));
            }

            if (string.IsNullOrWhiteSpace(prod.HsnSac))
            {
                exceptions.Add(new TriangulationExceptionDto(
                    "Warning",
                    "MissingHSN",
                    "PurchaseBill",
                    prod.BillNumber ?? "UNSPECIFIED",
                    prod.PurchaseDate,
                    vendor?.Name,
                    vendorGstin,
                    prod.PurchasePrice - prod.Discount,
                    $"Purchased product '{prod.SerialNumber}' has no HSN code assigned.",
                    "Tag HSN/SAC code in product master."
                ));
            }
        }

        // 3. Scan Journal Entries for Debit/Credit balancing errors
        var journals = await db.JournalEntries.AsNoTracking()
            .Where(j => j.JournalDate >= from && j.JournalDate <= to)
            .ToListAsync(ct);

        totalVouchersScanned += journals.Count;
        var journalIds = journals.Select(j => j.Id).ToList();

        var journalLines = await db.JournalLines.AsNoTracking()
            .Where(l => journalIds.Contains(l.JournalEntryId))
            .ToListAsync(ct);

        var linesByJournal = journalLines.GroupBy(l => l.JournalEntryId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var j in journals)
        {
            linesByJournal.TryGetValue(j.Id, out var lines);
            lines ??= [];
            var totalDr = lines.Sum(l => l.Debit);
            var totalCr = lines.Sum(l => l.Credit);
            var diff = Math.Abs(totalDr - totalCr);

            if (diff > 0.01m)
            {
                exceptions.Add(new TriangulationExceptionDto(
                    "Critical",
                    "MismatchedLedgers",
                    "JournalEntry",
                    j.JournalNumber,
                    j.JournalDate,
                    null,
                    null,
                    diff,
                    $"Journal Entry #{j.JournalNumber} has unbalanced Debits (₹{totalDr:F2}) vs Credits (₹{totalCr:F2}).",
                    "Post an adjusting journal line or rectify mismatched legs before statutory return filing."
                ));
            }
        }

        var criticalCount = exceptions.Count(e => e.Severity == "Critical");
        var warningCount = exceptions.Count(e => e.Severity == "Warning");
        var isReady = criticalCount == 0;

        return new PreFilingTriangulationResultDto(
            tenantGstin,
            from,
            to,
            totalVouchersScanned,
            exceptions.Count,
            criticalCount,
            warningCount,
            isReady,
            exceptions.OrderBy(e => e.Severity == "Critical" ? 0 : 1).ThenBy(e => e.VoucherNumber).ToList()
        );
    }
}
