using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Taxes.Common;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr1Summary;

public sealed class GetGstr1SummaryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetGstr1SummaryQuery, Gstr1SummaryDto>
{
    public async Task<Gstr1SummaryDto> Handle(GetGstr1SummaryQuery request, CancellationToken ct)
    {
        var to = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.FromDate ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) throw new ConflictException("From date must be on or before to date.");

        // Fetch company profile / tenant bill settings
        var settings = await db.CustomerBillSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == CustomerBillSettings.TenantSettingsId, ct);

        var tenantGstin = settings?.TaxRegistrationNumber ?? "27AABCU9603R1ZM";
        var tenantLegalName = settings?.CompanyName ?? "Store Entity";
        var supplierStateCode = GstReturnHelper.ResolveStateCode(tenantGstin, settings?.StateCode, settings?.StateName);
        var returnPeriod = to.ToGstPeriodFormat();

        // 1. Fetch Multi-line Sales Invoices in date range
        var multiInvoices = await db.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync(ct);

        // Fetch Customers for customer details
        var customerIds = multiInvoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        // Also fetch legacy SalesProducts in case some exist
        var legacySales = await db.SalesProducts.AsNoTracking()
            .Where(s => !s.IsDeleted && s.SaleDate >= from && s.SaleDate <= to)
            .ToListAsync(ct);

        // Fetch InvoiceCorrections (Credit/Debit Notes)
        var creditNotes = await db.InvoiceCorrections.AsNoTracking()
            .Where(n => n.NoteDate >= from && n.NoteDate <= to && n.Kind == "Sale")
            .ToListAsync(ct);

        // Prepare Table 4 (B2B), Table 5 (B2C Large), Table 7 (B2C Small)
        var b2bGroups = new List<Gstr1B2bInvoiceGroupDto>();
        var b2cLargeList = new List<Gstr1B2cLargeInvoiceDto>();
        var b2cSmallMap = new Dictionary<(string Pos, decimal Rate, string SupplyType), (decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst)>();

        // Process Multi-Line Sales Invoices
        var activeInvoices = multiInvoices.Where(i => !i.IsCancelled).ToList();

        // Group active invoices into B2B, B2CL, B2CS
        var b2bInvoicesByGstin = new Dictionary<string, (string Name, List<Gstr1B2bInvoiceItemDto> Invoices)>();

        foreach (var inv in activeInvoices)
        {
            customers.TryGetValue(inv.CustomerId, out var customer);
            var custGstin = !string.IsNullOrWhiteSpace(inv.CustomerGstin)
                ? inv.CustomerGstin
                : customer?.Gstin;
            var custName = customer?.Name ?? "Customer";
            var pos = GstReturnHelper.ResolveStateCode(custGstin, inv.PlaceOfSupplyStateCode ?? customer?.StateCode, inv.PlaceOfSupplyStateName ?? customer?.StateName);
            var isInterState = !string.Equals(pos, supplierStateCode, StringComparison.OrdinalIgnoreCase);

            var linesDto = inv.Lines.Select((line, index) =>
            {
                var rate = isInterState ? line.IgstRate : (line.CgstRate + line.SgstRate);
                return new Gstr1InvoiceLineDto(
                    index + 1,
                    new Gstr1ItemDetailDto(rate, line.TaxableAmount, line.CgstAmount, line.SgstAmount, line.IgstAmount)
                );
            }).ToList();

            if (GstReturnHelper.IsValidGstin(custGstin))
            {
                // B2B Invoice
                var b2bItem = new Gstr1B2bInvoiceItemDto(
                    inv.BillNumber,
                    inv.InvoiceDate,
                    inv.TotalAmount,
                    pos,
                    "N",
                    "Regular",
                    null,
                    linesDto
                );

                if (!b2bInvoicesByGstin.TryGetValue(custGstin!, out var group))
                {
                    group = (custName, new List<Gstr1B2bInvoiceItemDto>());
                    b2bInvoicesByGstin[custGstin!] = group;
                }
                group.Invoices.Add(b2bItem);
            }
            else
            {
                // Unregistered Customer
                if (isInterState && inv.TotalAmount > GstReturnHelper.B2CLargeThreshold)
                {
                    // B2C Large (Table 5)
                    var primaryRate = linesDto.Count > 0 ? linesDto[0].ItemDetails.Rate : 18m;
                    b2cLargeList.Add(new Gstr1B2cLargeInvoiceDto(
                        inv.BillNumber,
                        inv.InvoiceDate,
                        inv.TotalAmount,
                        pos,
                        null,
                        primaryRate,
                        linesDto
                    ));
                }
                else
                {
                    // B2C Small (Table 7) - grouped by POS and Rate
                    var supplyTypeStr = isInterState ? "InterState" : "IntraState";
                    foreach (var line in inv.Lines)
                    {
                        var rate = isInterState ? line.IgstRate : (line.CgstRate + line.SgstRate);
                        var key = (pos, rate, supplyTypeStr);
                        if (!b2cSmallMap.TryGetValue(key, out var current))
                        {
                            current = (0m, 0m, 0m, 0m);
                        }
                        b2cSmallMap[key] = (
                            current.Taxable + line.TaxableAmount,
                            current.Cgst + line.CgstAmount,
                            current.Sgst + line.SgstAmount,
                            current.Igst + line.IgstAmount
                        );
                    }
                }
            }
        }

        // Process legacy SalesProducts if any exist
        foreach (var legacy in legacySales)
        {
            var pos = supplierStateCode;
            var key = (pos, legacy.CgstRate + legacy.SgstRate, "IntraState");
            if (!b2cSmallMap.TryGetValue(key, out var current))
            {
                current = (0m, 0m, 0m, 0m);
            }
            b2cSmallMap[key] = (
                current.Taxable + legacy.TaxableAmount,
                current.Cgst + legacy.CgstAmount,
                current.Sgst + legacy.SgstAmount,
                current.Igst
            );
        }

        // Compile B2B Groups
        b2bGroups = b2bInvoicesByGstin.Select(kv => new Gstr1B2bInvoiceGroupDto(
            kv.Key,
            kv.Value.Name,
            kv.Value.Invoices
        )).OrderBy(g => g.CustomerGstin).ToList();

        // Compile B2C Small
        var b2cSmallList = b2cSmallMap.Select(kv => new Gstr1B2cSmallSummaryDto(
            kv.Key.Pos,
            kv.Key.SupplyType,
            kv.Key.Rate,
            kv.Value.Taxable,
            kv.Value.Cgst,
            kv.Value.Sgst,
            kv.Value.Igst
        )).OrderBy(x => x.PlaceOfSupply).ThenBy(x => x.Rate).ToList();

        // 4. Process Credit / Debit Notes (Table 9B CDNR & CDNUR)
        var cdnrGroups = new Dictionary<string, (string Name, List<Gstr1CdnrItemDto> Notes)>();
        var cdnumList = new List<Gstr1CdnumInvoiceDto>();

        var originalInvoiceIds = creditNotes.Select(cn => cn.SourceId).Distinct().ToList();
        var origInvoices = await db.SalesInvoices.AsNoTracking()
            .Where(i => originalInvoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        foreach (var note in creditNotes)
        {
            origInvoices.TryGetValue(note.SourceId, out var orig);
            customers.TryGetValue(note.PartyId, out var party);

            var custGstin = !string.IsNullOrWhiteSpace(orig?.CustomerGstin) ? orig.CustomerGstin : party?.Gstin;
            var custName = party?.Name ?? "Party";
            var pos = GstReturnHelper.ResolveStateCode(custGstin, orig?.PlaceOfSupplyStateCode ?? party?.StateCode, orig?.PlaceOfSupplyStateName ?? party?.StateName);
            var isInter = !string.Equals(pos, supplierStateCode, StringComparison.OrdinalIgnoreCase);
            var rate = isInter ? (note.CgstRate + note.SgstRate) : (note.CgstRate + note.SgstRate);

            var noteLine = new Gstr1InvoiceLineDto(
                1,
                new Gstr1ItemDetailDto(rate, note.TaxableAmount, note.CgstAmount, note.SgstAmount, note.IgstAmount)
            );

            var origDate = orig?.InvoiceDate ?? note.NoteDate;

            if (GstReturnHelper.IsValidGstin(custGstin))
            {
                var cdnrItem = new Gstr1CdnrItemDto(
                    note.NoteNumber,
                    note.NoteDate,
                    "C",
                    note.BillNumber,
                    origDate,
                    note.TotalAmount,
                    pos,
                    "N",
                    [noteLine]
                );

                if (!cdnrGroups.TryGetValue(custGstin!, out var grp))
                {
                    grp = (custName, new List<Gstr1CdnrItemDto>());
                    cdnrGroups[custGstin!] = grp;
                }
                grp.Notes.Add(cdnrItem);
            }
            else
            {
                cdnumList.Add(new Gstr1CdnumInvoiceDto(
                    note.NoteNumber,
                    note.NoteDate,
                    "C",
                    note.BillNumber,
                    origDate,
                    note.TotalAmount,
                    pos,
                    [noteLine]
                ));
            }
        }

        var cdnrList = cdnrGroups.Select(kv => new Gstr1CdnrInvoiceGroupDto(
            kv.Key,
            kv.Value.Name,
            kv.Value.Notes
        )).OrderBy(g => g.CustomerGstin).ToList();

        // 5. Table 12: HSN Summary of Outward Supplies
        var hsnMap = new Dictionary<string, (string Desc, string Uqc, decimal Qty, decimal Val, decimal Taxable, decimal Igst, decimal Cgst, decimal Sgst)>();

        foreach (var inv in activeInvoices)
        {
            foreach (var line in inv.Lines)
            {
                var hsn = string.IsNullOrWhiteSpace(line.HsnSac) ? "998313" : line.HsnSac.Trim();
                var uqc = GstReturnHelper.NormalizeUqc(line.UnitOfMeasure);
                var desc = line.ItemDescription ?? "Supply of goods / services";

                if (!hsnMap.TryGetValue(hsn, out var acc))
                {
                    acc = (desc, uqc, 0m, 0m, 0m, 0m, 0m, 0m);
                }

                hsnMap[hsn] = (
                    acc.Desc,
                    acc.Uqc,
                    acc.Qty + line.Quantity,
                    acc.Val + line.TotalAmount,
                    acc.Taxable + line.TaxableAmount,
                    acc.Igst + line.IgstAmount,
                    acc.Cgst + line.CgstAmount,
                    acc.Sgst + line.SgstAmount
                );
            }
        }

        var hsnSerial = 1;
        var hsnSummaries = hsnMap.Select(kv => new Gstr1HsnSummaryDto(
            hsnSerial++,
            kv.Key,
            kv.Value.Desc,
            kv.Value.Uqc,
            kv.Value.Qty,
            kv.Value.Val,
            kv.Value.Taxable,
            kv.Value.Igst,
            kv.Value.Cgst,
            kv.Value.Sgst
        )).OrderBy(h => h.HsnCode).ToList();

        // 6. Table 13: Documents Issued Summary
        var docIssuedList = new List<Gstr1DocIssuedDetailDto>();
        if (multiInvoices.Count > 0)
        {
            var sortedBills = multiInvoices.OrderBy(i => i.BillNumber).ToList();
            var fromBill = sortedBills.First().BillNumber;
            var toBill = sortedBills.Last().BillNumber;
            var cancelledCount = multiInvoices.Count(i => i.IsCancelled);
            var totalCount = multiInvoices.Count;

            docIssuedList.Add(new Gstr1DocIssuedDetailDto(
                1,
                "Invoices for outward supply",
                fromBill,
                toBill,
                totalCount,
                cancelledCount,
                totalCount - cancelledCount
            ));
        }

        if (creditNotes.Count > 0)
        {
            var sortedNotes = creditNotes.OrderBy(n => n.NoteNumber).ToList();
            docIssuedList.Add(new Gstr1DocIssuedDetailDto(
                2,
                "Credit Note",
                sortedNotes.First().NoteNumber,
                sortedNotes.Last().NoteNumber,
                creditNotes.Count,
                0,
                creditNotes.Count
            ));
        }

        var docIssued = new Gstr1DocIssuedSummaryDto(docIssuedList);

        // Overall Totals
        var totalTaxable = b2bGroups.Sum(g => g.Invoices.Sum(i => i.Items.Sum(item => item.ItemDetails.TaxableValue)))
                           + b2cLargeList.Sum(i => i.Items.Sum(item => item.ItemDetails.TaxableValue))
                           + b2cSmallList.Sum(s => s.TaxableValue);

        var totalCgst = b2bGroups.Sum(g => g.Invoices.Sum(i => i.Items.Sum(item => item.ItemDetails.CgstAmount)))
                        + b2cLargeList.Sum(i => i.Items.Sum(item => item.ItemDetails.CgstAmount))
                        + b2cSmallList.Sum(s => s.CgstAmount);

        var totalSgst = b2bGroups.Sum(g => g.Invoices.Sum(i => i.Items.Sum(item => item.ItemDetails.SgstAmount)))
                        + b2cLargeList.Sum(i => i.Items.Sum(item => item.ItemDetails.SgstAmount))
                        + b2cSmallList.Sum(s => s.SgstAmount);

        var totalIgst = b2bGroups.Sum(g => g.Invoices.Sum(i => i.Items.Sum(item => item.ItemDetails.IgstAmount)))
                        + b2cLargeList.Sum(i => i.Items.Sum(item => item.ItemDetails.IgstAmount))
                        + b2cSmallList.Sum(s => s.IgstAmount);

        var totalTax = totalCgst + totalSgst + totalIgst;
        var grossTurnover = totalTaxable + totalTax;

        return new Gstr1SummaryDto(
            tenantGstin,
            tenantLegalName,
            returnPeriod,
            from,
            to,
            grossTurnover,
            totalTaxable,
            totalTax,
            totalCgst,
            totalSgst,
            totalIgst,
            0m,
            b2bGroups,
            b2cLargeList,
            b2cSmallList,
            cdnrList,
            cdnumList,
            hsnSummaries,
            docIssued
        );
    }
}

