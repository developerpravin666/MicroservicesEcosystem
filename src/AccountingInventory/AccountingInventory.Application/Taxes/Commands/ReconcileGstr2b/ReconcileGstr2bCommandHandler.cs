using System.Globalization;
using System.Text.Json;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Commands.ReconcileGstr2b;

public sealed class ReconcileGstr2bCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<ReconcileGstr2bCommand, Gstr2bReconciliationResultDto>
{
    private sealed record PortalInvoiceRecord(
        string SupplierGstin,
        string SupplierName,
        string InvoiceNumber,
        DateOnly? InvoiceDate,
        decimal TaxableValue,
        decimal TaxAmount,
        decimal Cgst,
        decimal Sgst,
        decimal Igst
    );

    public async Task<Gstr2bReconciliationResultDto> Handle(ReconcileGstr2bCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.JsonContent))
            throw new ArgumentException("GSTR-2B JSON payload cannot be empty.");

        var settings = await db.CustomerBillSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == CustomerBillSettings.TenantSettingsId, ct);

        var tenantGstin = settings?.TaxRegistrationNumber ?? "27AABCU9603R1ZM";

        // 1. Parse uploaded GSTR-2B JSON
        var (portalGstin, returnPeriod, portalInvoices) = ParsePortalGstr2b(request.JsonContent);

        // Derive date range if not explicitly provided
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.FromDate ?? new DateOnly(to.Year, to.Month, 1);

        // 2. Fetch local books purchases
        var vendors = await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, ct);

        var products = await db.Products.AsNoTracking()
            .Where(p => !p.IsOpeningStock && p.PurchaseDate >= from && p.PurchaseDate <= to)
            .ToListAsync(ct);

        var skuMovements = await db.SkuMovements.AsNoTracking()
            .Where(m => m.Kind == "Purchase" && m.MovementDate >= from && m.MovementDate <= to)
            .ToListAsync(ct);

        // Group local books by (NormalizedBillNumber, SupplierGstin)
        var localBillMap = new Dictionary<string, (string Gstin, string VendorName, string BillNo, DateOnly Date, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst)>();

        foreach (var p in products)
        {
            if (string.IsNullOrWhiteSpace(p.BillNumber)) continue;
            var billNorm = p.BillNumber.Trim().ToUpperInvariant();
            vendors.TryGetValue(p.VendorId, out var vendor);
            var gstin = (vendor?.GstNumber ?? "").Trim().ToUpperInvariant();
            var key = $"{gstin}::{billNorm}";

            var taxable = p.PurchasePrice - p.Discount;
            var cgst = decimal.Round(taxable * p.Cgst / 100m, 2, MidpointRounding.AwayFromZero);
            var sgst = decimal.Round(taxable * p.Sgst / 100m, 2, MidpointRounding.AwayFromZero);
            var igst = 0m;

            if (!localBillMap.TryGetValue(key, out var current))
            {
                current = (gstin, vendor?.Name ?? "Vendor", p.BillNumber.Trim(), p.PurchaseDate, 0m, 0m, 0m, 0m);
            }

            localBillMap[key] = (
                current.Gstin,
                current.VendorName,
                current.BillNo,
                current.Date,
                current.Taxable + taxable,
                current.Cgst + cgst,
                current.Sgst + sgst,
                current.Igst + igst
            );
        }

        foreach (var m in skuMovements)
        {
            if (string.IsNullOrWhiteSpace(m.BillNumber)) continue;
            var billNorm = m.BillNumber.Trim().ToUpperInvariant();
            vendors.TryGetValue(m.VendorId ?? Guid.Empty, out var vendor);
            var gstin = (vendor?.GstNumber ?? "").Trim().ToUpperInvariant();
            var key = $"{gstin}::{billNorm}";

            if (!localBillMap.TryGetValue(key, out var current))
            {
                current = (gstin, vendor?.Name ?? "Accessory Vendor", m.BillNumber.Trim(), m.MovementDate, 0m, 0m, 0m, 0m);
            }

            localBillMap[key] = (
                current.Gstin,
                current.VendorName,
                current.BillNo,
                current.Date,
                current.Taxable + m.InventoryValue,
                current.Cgst + m.CgstAmount,
                current.Sgst + m.SgstAmount,
                current.Igst + m.IgstAmount
            );
        }

        // 3. Matching Algorithm
        var reconciledItems = new List<Gstr2bReconciliationItemDto>();
        var matchedLocalKeys = new HashSet<string>();

        foreach (var portalInv in portalInvoices)
        {
            var portalBillNorm = portalInv.InvoiceNumber.Trim().ToUpperInvariant();
            var portalGstinNorm = portalInv.SupplierGstin.Trim().ToUpperInvariant();

            // Try match by (SupplierGstin + BillNumber) or by (BillNumber) fallback
            var matchedKey = localBillMap.Keys.FirstOrDefault(k =>
                string.Equals(k, $"{portalGstinNorm}::{portalBillNorm}", StringComparison.OrdinalIgnoreCase) ||
                k.EndsWith($"::{portalBillNorm}", StringComparison.OrdinalIgnoreCase));

            if (matchedKey != null && localBillMap.TryGetValue(matchedKey, out var local))
            {
                matchedLocalKeys.Add(matchedKey);

                var localTaxTotal = local.Cgst + local.Sgst + local.Igst;
                var portalTaxTotal = portalInv.TaxAmount;

                var taxableDiff = Math.Abs(local.Taxable - portalInv.TaxableValue);
                var taxDiff = Math.Abs(localTaxTotal - portalTaxTotal);

                // Discrepancy tolerance of ₹1.00 for rounding differences
                if (taxableDiff <= 1.00m && taxDiff <= 1.00m)
                {
                    reconciledItems.Add(new Gstr2bReconciliationItemDto(
                        "MATCHED",
                        portalInv.SupplierGstin,
                        !string.IsNullOrWhiteSpace(portalInv.SupplierName) ? portalInv.SupplierName : local.VendorName,
                        portalInv.InvoiceNumber,
                        portalInv.InvoiceDate ?? local.Date,
                        local.Taxable,
                        localTaxTotal,
                        local.Cgst,
                        local.Sgst,
                        local.Igst,
                        portalInv.TaxableValue,
                        portalTaxTotal,
                        portalInv.Cgst,
                        portalInv.Sgst,
                        portalInv.Igst,
                        0m,
                        "100% Matched with Books (Safe to claim ITC)"
                    ));
                }
                else
                {
                    var diff = (local.Taxable + localTaxTotal) - (portalInv.TaxableValue + portalTaxTotal);
                    reconciledItems.Add(new Gstr2bReconciliationItemDto(
                        "VALUE_MISMATCH",
                        portalInv.SupplierGstin,
                        !string.IsNullOrWhiteSpace(portalInv.SupplierName) ? portalInv.SupplierName : local.VendorName,
                        portalInv.InvoiceNumber,
                        portalInv.InvoiceDate ?? local.Date,
                        local.Taxable,
                        localTaxTotal,
                        local.Cgst,
                        local.Sgst,
                        local.Igst,
                        portalInv.TaxableValue,
                        portalTaxTotal,
                        portalInv.Cgst,
                        portalInv.Sgst,
                        portalInv.Igst,
                        diff,
                        $"Taxable difference: ₹{taxableDiff:F2}, Tax difference: ₹{taxDiff:F2}"
                    ));
                }
            }
            else
            {
                // In Portal, Not in Books
                reconciledItems.Add(new Gstr2bReconciliationItemDto(
                    "PORTAL_ONLY",
                    portalInv.SupplierGstin,
                    portalInv.SupplierName,
                    portalInv.InvoiceNumber,
                    portalInv.InvoiceDate,
                    0m, 0m, 0m, 0m, 0m,
                    portalInv.TaxableValue,
                    portalInv.TaxAmount,
                    portalInv.Cgst,
                    portalInv.Sgst,
                    portalInv.Igst,
                    portalInv.TaxableValue + portalInv.TaxAmount,
                    "Supplier filed in GSTR-1, but invoice not recorded in local books"
                ));
            }
        }

        // Remaining in Books, Not in Portal
        foreach (var kv in localBillMap)
        {
            if (!matchedLocalKeys.Contains(kv.Key))
            {
                var local = kv.Value;
                var localTaxTotal = local.Cgst + local.Sgst + local.Igst;
                reconciledItems.Add(new Gstr2bReconciliationItemDto(
                    "BOOKS_ONLY",
                    local.Gstin,
                    local.VendorName,
                    local.BillNo,
                    local.Date,
                    local.Taxable,
                    localTaxTotal,
                    local.Cgst,
                    local.Sgst,
                    local.Igst,
                    0m, 0m, 0m, 0m, 0m,
                    local.Taxable + localTaxTotal,
                    "Recorded locally, but supplier did not file or missed GSTR-1"
                ));
            }
        }

        // Aggregate statistics
        var matchedCount = reconciledItems.Count(i => i.Status == "MATCHED");
        var booksOnlyCount = reconciledItems.Count(i => i.Status == "BOOKS_ONLY");
        var portalOnlyCount = reconciledItems.Count(i => i.Status == "PORTAL_ONLY");
        var valueMismatchCount = reconciledItems.Count(i => i.Status == "VALUE_MISMATCH");

        var totalMatchedItc = reconciledItems.Where(i => i.Status == "MATCHED").Sum(i => i.PortalTaxAmount);
        var totalBooksOnlyItc = reconciledItems.Where(i => i.Status == "BOOKS_ONLY").Sum(i => i.BooksTaxAmount);
        var totalPortalOnlyItc = reconciledItems.Where(i => i.Status == "PORTAL_ONLY").Sum(i => i.PortalTaxAmount);
        var totalValueMismatchDiff = reconciledItems.Where(i => i.Status == "VALUE_MISMATCH").Sum(i => Math.Abs(i.DifferenceAmount));

        return new Gstr2bReconciliationResultDto(
            !string.IsNullOrWhiteSpace(portalGstin) ? portalGstin : tenantGstin,
            returnPeriod,
            from,
            to,
            reconciledItems.Count,
            matchedCount,
            booksOnlyCount,
            portalOnlyCount,
            valueMismatchCount,
            totalMatchedItc,
            totalBooksOnlyItc,
            totalPortalOnlyItc,
            totalValueMismatchDiff,
            reconciledItems.OrderBy(i => i.Status switch
            {
                "VALUE_MISMATCH" => 1,
                "BOOKS_ONLY" => 2,
                "PORTAL_ONLY" => 3,
                _ => 4
            }).ThenBy(i => i.SupplierName).ToList()
        );
    }

    private static (string Gstin, string Period, List<PortalInvoiceRecord> Invoices) ParsePortalGstr2b(string json)
    {
        var invoices = new List<PortalInvoiceRecord>();
        var gstin = "";
        var period = "";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("gstin", out var gstinProp))
                gstin = gstinProp.GetString() ?? "";
            if (root.TryGetProperty("fp", out var fpProp))
                period = fpProp.GetString() ?? "";

            // Check if standard GSTR-2B JSON: { data: { docdata: { b2b: [...] } } } or direct { b2b: [...] }
            var b2bArray = default(JsonElement);
            var found = false;

            if (root.TryGetProperty("b2b", out b2bArray) && b2bArray.ValueKind == JsonValueKind.Array)
            {
                found = true;
            }
            else if (root.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("docdata", out var docDataProp) &&
                    docDataProp.TryGetProperty("b2b", out b2bArray) && b2bArray.ValueKind == JsonValueKind.Array)
                {
                    found = true;
                }
            }

            if (found)
            {
                foreach (var supplierGroup in b2bArray.EnumerateArray())
                {
                    var supplierGstin = supplierGroup.TryGetProperty("ctin", out var ctinProp) ? ctinProp.GetString() ?? "" : "";
                    var supplierName = supplierGroup.TryGetProperty("trdnm", out var trdnmProp) ? trdnmProp.GetString() ?? "" : "";

                    if (supplierGroup.TryGetProperty("inv", out var invArray) && invArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var inv in invArray.EnumerateArray())
                        {
                            var invNum = inv.TryGetProperty("inum", out var inumProp) ? inumProp.GetString() ?? "" : "";
                            var invDtStr = inv.TryGetProperty("dt", out var dtProp) ? dtProp.GetString() :
                                           inv.TryGetProperty("idt", out var idtProp) ? idtProp.GetString() : null;

                            DateOnly? invDate = null;
                            if (!string.IsNullOrWhiteSpace(invDtStr))
                            {
                                if (DateOnly.TryParseExact(invDtStr, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                                    invDate = d1;
                                else if (DateOnly.TryParse(invDtStr, out var d2))
                                    invDate = d2;
                            }

                            decimal txval = 0m, igst = 0m, cgst = 0m, sgst = 0m;

                            if (inv.TryGetProperty("items", out var itmArray) && itmArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var itm in itmArray.EnumerateArray())
                                {
                                    if (itm.TryGetProperty("txval", out var txProp)) txval += txProp.GetDecimal();
                                    if (itm.TryGetProperty("iamt", out var iamtProp)) igst += iamtProp.GetDecimal();
                                    if (itm.TryGetProperty("camt", out var camtProp)) cgst += camtProp.GetDecimal();
                                    if (itm.TryGetProperty("samt", out var samtProp)) sgst += samtProp.GetDecimal();
                                }
                            }
                            else if (inv.TryGetProperty("val", out var valProp))
                            {
                                txval = valProp.GetDecimal();
                            }

                            var totalTax = igst + cgst + sgst;
                            invoices.Add(new PortalInvoiceRecord(supplierGstin, supplierName, invNum, invDate, txval, totalTax, cgst, sgst, igst));
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid GSTR-2B JSON format: {ex.Message}");
        }

        return (gstin, period, invoices);
    }
}
