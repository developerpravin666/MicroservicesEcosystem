using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Taxes.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr3bSummary;

public sealed class GetGstr3bSummaryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetGstr3bSummaryQuery, Gstr3bSummaryDto>
{
    public async Task<Gstr3bSummaryDto> Handle(GetGstr3bSummaryQuery request, CancellationToken ct)
    {
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.FromDate ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) throw new ConflictException("From date must be on or before to date.");

        var settings = await db.CustomerBillSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == CustomerBillSettings.TenantSettingsId, ct);

        var tenantGstin = settings?.TaxRegistrationNumber ?? "27AABCU9603R1ZM";
        var tenantLegalName = settings?.CompanyName ?? "Store Entity";
        var returnPeriod = to.ToGstPeriodFormat();

        // 1. OUTWARD SUPPLIES (Table 3.1)
        // Multi-line sales
        var multiInvoices = await db.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => !i.IsCancelled && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync(ct);

        // Single legacy sales
        var legacySales = await db.SalesProducts.AsNoTracking()
            .Where(s => !s.IsDeleted && s.SaleDate >= from && s.SaleDate <= to)
            .ToListAsync(ct);

        // Sales Credit Notes
        var salesNotes = await db.InvoiceCorrections.AsNoTracking()
            .Where(n => n.NoteDate >= from && n.NoteDate <= to && n.Kind == "Sale")
            .ToListAsync(ct);

        decimal outTaxable = 0m, outCgst = 0m, outSgst = 0m, outIgst = 0m;
        decimal outZeroTaxable = 0m, outNilTaxable = 0m, nonGstTaxable = 0m;

        foreach (var inv in multiInvoices)
        {
            foreach (var line in inv.Lines)
            {
                var treatment = line.TaxTreatment ?? "Taxable";
                if (treatment == "ZeroRated")
                {
                    outZeroTaxable += line.TaxableAmount;
                }
                else if (treatment is "NilRated" or "Exempt")
                {
                    outNilTaxable += line.TaxableAmount;
                }
                else if (treatment == "NonGst")
                {
                    nonGstTaxable += line.TaxableAmount;
                }
                else
                {
                    outTaxable += line.TaxableAmount;
                    outCgst += line.CgstAmount;
                    outSgst += line.SgstAmount;
                    outIgst += line.IgstAmount;
                }
            }
        }

        foreach (var leg in legacySales)
        {
            var treatment = leg.TaxTreatment ?? "Taxable";
            if (treatment == "ZeroRated")
            {
                outZeroTaxable += leg.TaxableAmount;
            }
            else if (treatment is "NilRated" or "Exempt")
            {
                outNilTaxable += leg.TaxableAmount;
            }
            else if (treatment == "NonGst")
            {
                nonGstTaxable += leg.TaxableAmount;
            }
            else
            {
                outTaxable += leg.TaxableAmount;
                outCgst += leg.CgstAmount;
                outSgst += leg.SgstAmount;
            }
        }

        // Adjust for Credit Notes (deductions from outward taxable)
        foreach (var note in salesNotes)
        {
            outTaxable -= note.TaxableAmount;
            outCgst -= note.CgstAmount;
            outSgst -= note.SgstAmount;
            outIgst -= note.IgstAmount;
        }

        var outwardSuppliesTable = new Gstr3bTable31OutwardSuppliesDto(
            OutwardTaxableSupplies: new Gstr3bSupplyRowDto(
                "(a) Outward taxable supplies (other than zero rated, nil rated and exempted)",
                Math.Max(0m, outTaxable),
                Math.Max(0m, outIgst),
                Math.Max(0m, outCgst),
                Math.Max(0m, outSgst)
            ),
            OutwardZeroRatedSupplies: new Gstr3bSupplyRowDto(
                "(b) Outward taxable supplies (zero rated)",
                outZeroTaxable, 0m, 0m, 0m
            ),
            OtherOutwardNilExemptedSupplies: new Gstr3bSupplyRowDto(
                "(c) Other outward supplies (nil rated, exempted)",
                outNilTaxable, 0m, 0m, 0m
            ),
            InwardLiableToReverseCharge: new Gstr3bSupplyRowDto(
                "(d) Inward supplies (liable to reverse charge)",
                0m, 0m, 0m, 0m
            ),
            NonGstOutwardSupplies: new Gstr3bSupplyRowDto(
                "(e) Non-GST outward supplies",
                nonGstTaxable, 0m, 0m, 0m
            )
        );

        // 2. ELIGIBLE INPUT TAX CREDIT (Table 4)
        // Products purchased
        var purchaseProducts = await db.Products.AsNoTracking()
            .Where(p => !p.IsOpeningStock && p.PurchaseDate >= from && p.PurchaseDate <= to)
            .ToListAsync(ct);

        // Sku movements purchased
        var skuPurchases = await db.SkuMovements.AsNoTracking()
            .Where(m => m.Kind == "Purchase" && m.MovementDate >= from && m.MovementDate <= to)
            .ToListAsync(ct);

        // Purchase debit notes
        var purchaseNotes = await db.InvoiceCorrections.AsNoTracking()
            .Where(n => n.NoteDate >= from && n.NoteDate <= to && n.Kind == "Purchase")
            .ToListAsync(ct);

        decimal itcCgst = 0m, itcSgst = 0m, itcIgst = 0m;

        foreach (var p in purchaseProducts)
        {
            if (p.TaxTreatment == "Taxable")
            {
                var taxable = p.PurchasePrice - p.Discount;
                itcCgst += decimal.Round(taxable * p.Cgst / 100m, 2, MidpointRounding.AwayFromZero);
                itcSgst += decimal.Round(taxable * p.Sgst / 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        foreach (var sku in skuPurchases)
        {
            if (sku.TaxTreatment == "Taxable")
            {
                itcCgst += sku.CgstAmount;
                itcSgst += sku.SgstAmount;
                itcIgst += sku.IgstAmount;
            }
        }

        // Subtract purchase debit notes
        foreach (var pn in purchaseNotes)
        {
            itcCgst -= pn.CgstAmount;
            itcSgst -= pn.SgstAmount;
            itcIgst -= pn.IgstAmount;
        }

        itcCgst = Math.Max(0m, itcCgst);
        itcSgst = Math.Max(0m, itcSgst);
        itcIgst = Math.Max(0m, itcIgst);

        var itcAvailable = new List<Gstr3bItcRowDto>
        {
            new("(1) Import of goods", 0m, 0m, 0m),
            new("(2) Import of services", 0m, 0m, 0m),
            new("(3) Inward supplies liable to reverse charge", 0m, 0m, 0m),
            new("(4) Inward supplies from ISD", 0m, 0m, 0m),
            new("(5) All other ITC", itcIgst, itcCgst, itcSgst)
        };

        var itcReversed = new List<Gstr3bItcRowDto>
        {
            new("(1) As per rules 38, 42 & 43 of CGST Rules and section 17(5)", 0m, 0m, 0m),
            new("(2) Others", 0m, 0m, 0m)
        };

        var netItc = new Gstr3bItcRowDto("Net ITC Available (A) - (B)", itcIgst, itcCgst, itcSgst);

        var table4 = new Gstr3bTable4EligibleItcDto(itcAvailable, itcReversed, netItc);

        // 3. TABLE 5: INWARD SUPPLIES (Exempt, Nil, Non-GST)
        var exemptInwardInter = purchaseProducts.Where(p => p.TaxTreatment is "Exempt" or "NilRated")
            .Sum(p => p.PurchasePrice - p.Discount);
        var table5 = new Gstr3bTable5InwardSuppliesDto(0m, exemptInwardInter);

        // 4. TAX PAYMENT & ITC SET-OFF (Table 6)
        // Rule: IGST credit can offset IGST, then CGST, then SGST. CGST credit offsets CGST, then IGST. SGST credit offsets SGST, then IGST.
        // For standard retail computation:
        var igstPayable = outwardSuppliesTable.OutwardTaxableSupplies.IntegratedTax;
        var cgstPayable = outwardSuppliesTable.OutwardTaxableSupplies.CentralTax;
        var sgstPayable = outwardSuppliesTable.OutwardTaxableSupplies.StateTax;

        // IGST utilization
        var igstPaidItc = Math.Min(igstPayable, itcIgst);
        var remIgstItc = itcIgst - igstPaidItc;
        var igstCash = igstPayable - igstPaidItc;

        // CGST utilization
        var cgstPaidItc = Math.Min(cgstPayable, itcCgst + remIgstItc);
        var cgstCash = cgstPayable - cgstPaidItc;

        // SGST utilization
        var sgstPaidItc = Math.Min(sgstPayable, itcSgst);
        var sgstCash = sgstPayable - sgstPaidItc;

        var paymentRows = new List<Gstr3bTaxPaymentRowDto>
        {
            new("Integrated Tax", igstPayable, igstPaidItc, igstCash),
            new("Central Tax", cgstPayable, cgstPaidItc, cgstCash),
            new("State/UT Tax", sgstPayable, sgstPaidItc, sgstCash),
            new("Cess", 0m, 0m, 0m)
        };

        var totalLiability = igstPayable + cgstPayable + sgstPayable;
        var totalItcUsed = igstPaidItc + cgstPaidItc + sgstPaidItc;
        var netCash = igstCash + cgstCash + sgstCash;

        var taxPayment = new Gstr3bTaxPaymentSummaryDto(paymentRows, totalLiability, totalItcUsed, netCash);

        return new Gstr3bSummaryDto(
            tenantGstin,
            tenantLegalName,
            returnPeriod,
            from,
            to,
            outwardSuppliesTable,
            table4,
            table5,
            taxPayment
        );
    }
}

