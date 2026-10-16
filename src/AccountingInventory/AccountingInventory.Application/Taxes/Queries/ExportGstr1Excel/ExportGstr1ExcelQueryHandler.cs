using AccountingInventory.Application.Taxes.Common;
using AccountingInventory.Application.Taxes.Queries.GetGstr1Summary;
using ClosedXML.Excel;
using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Excel;

public sealed class ExportGstr1ExcelQueryHandler(ISender sender)
    : IRequestHandler<ExportGstr1ExcelQuery, Gstr1ExcelExportResult>
{
    public async Task<Gstr1ExcelExportResult> Handle(ExportGstr1ExcelQuery request, CancellationToken ct)
    {
        var summary = await sender.Send(new GetGstr1SummaryQuery(request.FromDate, request.ToDate), ct);

        using var workbook = new XLWorkbook();

        // 1. Sheet b2b
        var b2bSheet = workbook.Worksheets.Add("b2b");
        var b2bHeaders = new[]
        {
            "GSTIN/UIN of Recipient", "Receiver Name", "Invoice Number", "Invoice date", "Invoice Value",
            "Place Of Supply", "Reverse Charge", "Applicable % of Tax Rate", "Invoice Type", "E-Commerce GSTIN",
            "Rate", "Taxable Value", "Cess Amount"
        };
        WriteHeaders(b2bSheet, b2bHeaders);
        var b2bRow = 2;
        foreach (var grp in summary.B2bInvoices)
        {
            foreach (var inv in grp.Invoices)
            {
                foreach (var item in inv.Items)
                {
                    b2bSheet.Cell(b2bRow, 1).SetValue(grp.CustomerGstin);
                    b2bSheet.Cell(b2bRow, 2).SetValue(grp.CustomerLegalName);
                    b2bSheet.Cell(b2bRow, 3).SetValue(inv.InvoiceNumber);
                    b2bSheet.Cell(b2bRow, 4).SetValue(inv.InvoiceDate.ToGstDateFormat());
                    b2bSheet.Cell(b2bRow, 5).SetValue(inv.InvoiceValue);
                    b2bSheet.Cell(b2bRow, 6).SetValue(inv.PlaceOfSupply);
                    b2bSheet.Cell(b2bRow, 7).SetValue(inv.ReverseCharge);
                    b2bSheet.Cell(b2bRow, 8).SetValue("");
                    b2bSheet.Cell(b2bRow, 9).SetValue(inv.InvoiceType);
                    b2bSheet.Cell(b2bRow, 10).SetValue(inv.EcommerceGstin ?? "");
                    b2bSheet.Cell(b2bRow, 11).SetValue(item.ItemDetails.Rate);
                    b2bSheet.Cell(b2bRow, 12).SetValue(item.ItemDetails.TaxableValue);
                    b2bSheet.Cell(b2bRow, 13).SetValue(item.ItemDetails.CessAmount);
                    b2bRow++;
                }
            }
        }

        // 2. Sheet b2cl
        var b2clSheet = workbook.Worksheets.Add("b2cl");
        var b2clHeaders = new[]
        {
            "Invoice Number", "Invoice date", "Invoice Value", "Place Of Supply", "Applicable % of Tax Rate",
            "Rate", "Taxable Value", "Cess Amount", "E-Commerce GSTIN"
        };
        WriteHeaders(b2clSheet, b2clHeaders);
        var b2clRow = 2;
        foreach (var inv in summary.B2cLargeInvoices)
        {
            foreach (var item in inv.Items)
            {
                b2clSheet.Cell(b2clRow, 1).SetValue(inv.InvoiceNumber);
                b2clSheet.Cell(b2clRow, 2).SetValue(inv.InvoiceDate.ToGstDateFormat());
                b2clSheet.Cell(b2clRow, 3).SetValue(inv.InvoiceValue);
                b2clSheet.Cell(b2clRow, 4).SetValue(inv.PlaceOfSupply);
                b2clSheet.Cell(b2clRow, 5).SetValue("");
                b2clSheet.Cell(b2clRow, 6).SetValue(item.ItemDetails.Rate);
                b2clSheet.Cell(b2clRow, 7).SetValue(item.ItemDetails.TaxableValue);
                b2clSheet.Cell(b2clRow, 8).SetValue(item.ItemDetails.CessAmount);
                b2clSheet.Cell(b2clRow, 9).SetValue(inv.EcommerceGstin ?? "");
                b2clRow++;
            }
        }

        // 3. Sheet b2cs
        var b2csSheet = workbook.Worksheets.Add("b2cs");
        var b2csHeaders = new[]
        {
            "Type", "Place Of Supply", "Applicable % of Tax Rate", "Rate", "Taxable Value",
            "Cess Amount", "E-Commerce GSTIN"
        };
        WriteHeaders(b2csSheet, b2csHeaders);
        var b2csRow = 2;
        foreach (var s in summary.B2cSmallSummaries)
        {
            b2csSheet.Cell(b2csRow, 1).SetValue("OE");
            b2csSheet.Cell(b2csRow, 2).SetValue(s.PlaceOfSupply);
            b2csSheet.Cell(b2csRow, 3).SetValue("");
            b2csSheet.Cell(b2csRow, 4).SetValue(s.Rate);
            b2csSheet.Cell(b2csRow, 5).SetValue(s.TaxableValue);
            b2csSheet.Cell(b2csRow, 6).SetValue(s.CessAmount);
            b2csSheet.Cell(b2csRow, 7).SetValue(s.EcommerceGstin ?? "");
            b2csRow++;
        }

        // 4. Sheet cdnr
        var cdnrSheet = workbook.Worksheets.Add("cdnr");
        var cdnrHeaders = new[]
        {
            "GSTIN/UIN of Recipient", "Receiver Name", "Note/Refund Voucher Number", "Note/Refund Voucher date",
            "Note/Refund Voucher Value", "Document Type", "Place Of Supply", "Reverse Charge",
            "Applicable % of Tax Rate", "Rate", "Taxable Value", "Cess Amount", "Pre GST"
        };
        WriteHeaders(cdnrSheet, cdnrHeaders);
        var cdnrRow = 2;
        foreach (var grp in summary.CreditDebitNotesRegistered)
        {
            foreach (var note in grp.Notes)
            {
                foreach (var item in note.Items)
                {
                    cdnrSheet.Cell(cdnrRow, 1).SetValue(grp.CustomerGstin);
                    cdnrSheet.Cell(cdnrRow, 2).SetValue(grp.CustomerLegalName);
                    cdnrSheet.Cell(cdnrRow, 3).SetValue(note.NoteNumber);
                    cdnrSheet.Cell(cdnrRow, 4).SetValue(note.NoteDate.ToGstDateFormat());
                    cdnrSheet.Cell(cdnrRow, 5).SetValue(note.NoteValue);
                    cdnrSheet.Cell(cdnrRow, 6).SetValue(note.NoteType);
                    cdnrSheet.Cell(cdnrRow, 7).SetValue(note.PlaceOfSupply);
                    cdnrSheet.Cell(cdnrRow, 8).SetValue(note.ReverseCharge);
                    cdnrSheet.Cell(cdnrRow, 9).SetValue("");
                    cdnrSheet.Cell(cdnrRow, 10).SetValue(item.ItemDetails.Rate);
                    cdnrSheet.Cell(cdnrRow, 11).SetValue(item.ItemDetails.TaxableValue);
                    cdnrSheet.Cell(cdnrRow, 12).SetValue(item.ItemDetails.CessAmount);
                    cdnrSheet.Cell(cdnrRow, 13).SetValue("N");
                    cdnrRow++;
                }
            }
        }

        // 5. Sheet hsn
        var hsnSheet = workbook.Worksheets.Add("hsn");
        var hsnHeaders = new[]
        {
            "HSN", "Description", "UQC", "Total Quantity", "Total Value",
            "Taxable Value", "Integrated Tax Amount", "Central Tax Amount", "State/UT Tax Amount", "Cess Amount"
        };
        WriteHeaders(hsnSheet, hsnHeaders);
        var hsnRow = 2;
        foreach (var h in summary.HsnSummaries)
        {
            hsnSheet.Cell(hsnRow, 1).SetValue(h.HsnCode);
            hsnSheet.Cell(hsnRow, 2).SetValue(h.Description);
            hsnSheet.Cell(hsnRow, 3).SetValue(h.Uqc);
            hsnSheet.Cell(hsnRow, 4).SetValue(h.TotalQuantity);
            hsnSheet.Cell(hsnRow, 5).SetValue(h.TotalValue);
            hsnSheet.Cell(hsnRow, 6).SetValue(h.TaxableValue);
            hsnSheet.Cell(hsnRow, 7).SetValue(h.IntegratedTax);
            hsnSheet.Cell(hsnRow, 8).SetValue(h.CentralTax);
            hsnSheet.Cell(hsnRow, 9).SetValue(h.StateTax);
            hsnSheet.Cell(hsnRow, 10).SetValue(h.Cess);
            hsnRow++;
        }

        // 6. Sheet docs
        var docSheet = workbook.Worksheets.Add("docs");
        var docHeaders = new[]
        {
            "Nature of Document", "Sr. No. From", "Sr. No. To", "Total Number", "Cancelled", "Net Issued"
        };
        WriteHeaders(docSheet, docHeaders);
        var docRow = 2;
        foreach (var d in summary.DocumentsIssued.DocumentDetails)
        {
            docSheet.Cell(docRow, 1).SetValue(d.DocNatureDescription);
            docSheet.Cell(docRow, 2).SetValue(d.FromSerialNumber);
            docSheet.Cell(docRow, 3).SetValue(d.ToSerialNumber);
            docSheet.Cell(docRow, 4).SetValue(d.TotalNumber);
            docSheet.Cell(docRow, 5).SetValue(d.CancelledNumber);
            docSheet.Cell(docRow, 6).SetValue(d.NetIssuedNumber);
            docRow++;
        }

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        var bytes = memoryStream.ToArray();
        var fileName = $"GSTR1_{summary.Gstin}_{summary.ReturnPeriod}.xlsx";

        return new Gstr1ExcelExportResult(fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes);
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.SetValue(headers[col]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
        }
    }
}

