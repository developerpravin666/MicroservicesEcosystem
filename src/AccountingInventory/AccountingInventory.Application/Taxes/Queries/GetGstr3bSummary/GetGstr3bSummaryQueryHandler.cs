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

        // RCM (Reverse Charge Mechanism) Invoices (Section 9(3) and 9(4))
        var rcmInvoices = await db.RcmSelfInvoices.AsNoTracking()
            .Where(r => r.Status == "Active" && r.VoucherDate >= from && r.VoucherDate <= to)
            .ToListAsync(ct);

        decimal rcmTaxable = rcmInvoices.Sum(r => r.TaxableAmount);
        decimal rcmCgst = rcmInvoices.Sum(r => r.CgstAmount);
        decimal rcmSgst = rcmInvoices.Sum(r => r.SgstAmount);
        decimal rcmIgst = rcmInvoices.Sum(r => r.IgstAmount);

        decimal rcmItcCgst = rcmInvoices.Where(r => r.IsItcEligible).Sum(r => r.CgstAmount);
        decimal rcmItcSgst = rcmInvoices.Where(r => r.IsItcEligible).Sum(r => r.SgstAmount);
        decimal rcmItcIgst = rcmInvoices.Where(r => r.IsItcEligible).Sum(r => r.IgstAmount);

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
                rcmTaxable, rcmIgst, rcmCgst, rcmSgst
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

        var totalItcIgst = itcIgst + rcmItcIgst;
        var totalItcCgst = itcCgst + rcmItcCgst;
        var totalItcSgst = itcSgst + rcmItcSgst;

        var itcAvailable = new List<Gstr3bItcRowDto>
        {
            new("(1) Import of goods", 0m, 0m, 0m),
            new("(2) Import of services", 0m, 0m, 0m),
            new("(3) Inward supplies liable to reverse charge", rcmItcIgst, rcmItcCgst, rcmItcSgst),
            new("(4) Inward supplies from ISD", 0m, 0m, 0m),
            new("(5) All other ITC", itcIgst, itcCgst, itcSgst)
        };

        var itcReversed = new List<Gstr3bItcRowDto>
        {
            new("(1) As per rules 38, 42 & 43 of CGST Rules and section 17(5)", 0m, 0m, 0m),
            new("(2) Others", 0m, 0m, 0m)
        };

        var netItc = new Gstr3bItcRowDto("Net ITC Available (A) - (B)", totalItcIgst, totalItcCgst, totalItcSgst);

        var table4 = new Gstr3bTable4EligibleItcDto(itcAvailable, itcReversed, netItc);

        // 3. TABLE 5: INWARD SUPPLIES (Exempt, Nil, Non-GST)
        var exemptInwardInter = purchaseProducts.Where(p => p.TaxTreatment is "Exempt" or "NilRated")
            .Sum(p => p.PurchasePrice - p.Discount);
        var table5 = new Gstr3bTable5InwardSuppliesDto(0m, exemptInwardInter);

        // 4. TAX PAYMENT & ITC SET-OFF (Table 6)
        // Rule: Output tax can be paid via ITC. Statutory mandate: RCM liability must be paid in CASH (Sec 49(4)).
        var outwardIgst = outwardSuppliesTable.OutwardTaxableSupplies.IntegratedTax;
        var outwardCgst = outwardSuppliesTable.OutwardTaxableSupplies.CentralTax;
        var outwardSgst = outwardSuppliesTable.OutwardTaxableSupplies.StateTax;

        // IGST utilization against outward
        var igstPaidItc = Math.Min(outwardIgst, totalItcIgst);
        var remIgstItc = totalItcIgst - igstPaidItc;
        var outwardIgstCash = outwardIgst - igstPaidItc;

        // CGST utilization against outward
        var cgstPaidItc = Math.Min(outwardCgst, totalItcCgst + remIgstItc);
        var remIgstAfterCgst = Math.Max(0m, remIgstItc - (cgstPaidItc - Math.Min(outwardCgst, totalItcCgst)));
        var outwardCgstCash = outwardCgst - cgstPaidItc;

        // SGST utilization against outward
        var sgstPaidItc = Math.Min(outwardSgst, totalItcSgst + remIgstAfterCgst);
        var outwardSgstCash = outwardSgst - sgstPaidItc;

        // RCM liabilities must be discharged in cash as per Section 49(4) of CGST Act
        var totalIgstPayable = outwardIgst + rcmIgst;
        var totalCgstPayable = outwardCgst + rcmCgst;
        var totalSgstPayable = outwardSgst + rcmSgst;

        var igstCashTotal = outwardIgstCash + rcmIgst;
        var cgstCashTotal = outwardCgstCash + rcmCgst;
        var sgstCashTotal = outwardSgstCash + rcmSgst;

        var paymentRows = new List<Gstr3bTaxPaymentRowDto>
        {
            new("Integrated Tax", totalIgstPayable, igstPaidItc, igstCashTotal),
            new("Central Tax", totalCgstPayable, cgstPaidItc, cgstCashTotal),
            new("State/UT Tax", totalSgstPayable, sgstPaidItc, sgstCashTotal),
            new("Cess", 0m, 0m, 0m)
        };

        var totalLiability = totalIgstPayable + totalCgstPayable + totalSgstPayable;
        var totalItcUsed = igstPaidItc + cgstPaidItc + sgstPaidItc;
        var netCash = igstCashTotal + cgstCashTotal + sgstCashTotal;

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

