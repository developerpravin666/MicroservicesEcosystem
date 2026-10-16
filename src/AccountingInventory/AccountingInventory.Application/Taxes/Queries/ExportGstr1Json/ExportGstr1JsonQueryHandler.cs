using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingInventory.Application.Taxes.Common;
using AccountingInventory.Application.Taxes.Queries.GetGstr1Summary;
using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Json;

public sealed class ExportGstr1JsonQueryHandler(ISender sender)
    : IRequestHandler<ExportGstr1JsonQuery, Gstr1JsonExportResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<Gstr1JsonExportResult> Handle(ExportGstr1JsonQuery request, CancellationToken ct)
    {
        var summary = await sender.Send(new GetGstr1SummaryQuery(request.FromDate, request.ToDate), ct);

        // Build Official GST Portal JSON Schema (v1.4) payload
        var b2bPayload = summary.B2bInvoices.Select(grp => new
        {
            ctin = grp.CustomerGstin,
            inv = grp.Invoices.Select(i => new
            {
                inum = i.InvoiceNumber,
                idt = i.InvoiceDate.ToGstDateFormat(),
                val = i.InvoiceValue,
                pos = i.PlaceOfSupply,
                rchrg = i.ReverseCharge,
                inv_typ = "R",
                itms = i.Items.Select(item => new
                {
                    num = item.LineNumber,
                    itm_det = new
                    {
                        rt = item.ItemDetails.Rate,
                        txval = item.ItemDetails.TaxableValue,
                        iamt = item.ItemDetails.IgstAmount,
                        camt = item.ItemDetails.CgstAmount,
                        samt = item.ItemDetails.SgstAmount,
                        csamt = item.ItemDetails.CessAmount
                    }
                }).ToArray()
            }).ToArray()
        }).ToArray();

        var b2clPayload = summary.B2cLargeInvoices.Select(i => new
        {
            pos = i.PlaceOfSupply,
            inv = new[]
            {
                new
                {
                    inum = i.InvoiceNumber,
                    idt = i.InvoiceDate.ToGstDateFormat(),
                    val = i.InvoiceValue,
                    itms = i.Items.Select(item => new
                    {
                        num = item.LineNumber,
                        itm_det = new
                        {
                            rt = item.ItemDetails.Rate,
                            txval = item.ItemDetails.TaxableValue,
                            iamt = item.ItemDetails.IgstAmount,
                            csamt = item.ItemDetails.CessAmount
                        }
                    }).ToArray()
                }
            }
        }).ToArray();

        var b2csPayload = summary.B2cSmallSummaries.Select(s => new
        {
            sply_ty = s.SupplyType == "InterState" ? "INTER" : "INTRA",
            pos = s.PlaceOfSupply,
            typ = "OE",
            rt = s.Rate,
            txval = s.TaxableValue,
            iamt = s.IgstAmount,
            camt = s.CgstAmount,
            samt = s.SgstAmount,
            csamt = s.CessAmount
        }).ToArray();

        var cdnrPayload = summary.CreditDebitNotesRegistered.Select(grp => new
        {
            ctin = grp.CustomerGstin,
            nt = grp.Notes.Select(n => new
            {
                nt_num = n.NoteNumber,
                nt_dt = n.NoteDate.ToGstDateFormat(),
                ntty = n.NoteType,
                inum = n.OriginalInvoiceNumber,
                idt = n.OriginalInvoiceDate.ToGstDateFormat(),
                val = n.NoteValue,
                pos = n.PlaceOfSupply,
                rchrg = n.ReverseCharge,
                itms = n.Items.Select(item => new
                {
                    num = item.LineNumber,
                    itm_det = new
                    {
                        rt = item.ItemDetails.Rate,
                        txval = item.ItemDetails.TaxableValue,
                        iamt = item.ItemDetails.IgstAmount,
                        camt = item.ItemDetails.CgstAmount,
                        samt = item.ItemDetails.SgstAmount,
                        csamt = item.ItemDetails.CessAmount
                    }
                }).ToArray()
            }).ToArray()
        }).ToArray();

        var cdnurPayload = summary.CreditDebitNotesUnregistered.Select(n => new
        {
            typ = "B2CL",
            nt_num = n.NoteNumber,
            nt_dt = n.NoteDate.ToGstDateFormat(),
            ntty = n.NoteType,
            inum = n.OriginalInvoiceNumber,
            idt = n.OriginalInvoiceDate.ToGstDateFormat(),
            val = n.NoteValue,
            pos = n.PlaceOfSupply,
            itms = n.Items.Select(item => new
            {
                num = item.LineNumber,
                itm_det = new
                {
                    rt = item.ItemDetails.Rate,
                    txval = item.ItemDetails.TaxableValue,
                    iamt = item.ItemDetails.IgstAmount,
                    camt = item.ItemDetails.CgstAmount,
                    samt = item.ItemDetails.SgstAmount,
                    csamt = item.ItemDetails.CessAmount
                }
            }).ToArray()
        }).ToArray();

        var hsnPayload = new
        {
            data = summary.HsnSummaries.Select(h => new
            {
                num = h.SerialNumber,
                hsn_sc = h.HsnCode,
                desc = h.Description,
                uqc = h.Uqc,
                qty = h.TotalQuantity,
                val = h.TotalValue,
                txval = h.TaxableValue,
                iamt = h.IntegratedTax,
                camt = h.CentralTax,
                samt = h.StateTax,
                csamt = h.Cess
            }).ToArray()
        };

        var docPayload = new
        {
            doc_det = summary.DocumentsIssued.DocumentDetails.Select(d => new
            {
                doc_num = d.DocNatureNumber,
                doc_typ = d.DocNatureDescription,
                from = d.FromSerialNumber,
                to = d.ToSerialNumber,
                totnum = d.TotalNumber,
                canc = d.CancelledNumber,
                net_issue = d.NetIssuedNumber
            }).ToArray()
        };

        var portalJson = new
        {
            gstin = summary.Gstin,
            fp = summary.ReturnPeriod,
            gt = summary.GrossTurnover,
            cur_gt = summary.GrossTurnover,
            version = "GSTR1_Excel_V1.4",
            b2b = b2bPayload.Length > 0 ? b2bPayload : null,
            b2cl = b2clPayload.Length > 0 ? b2clPayload : null,
            b2cs = b2csPayload.Length > 0 ? b2csPayload : null,
            cdnr = cdnrPayload.Length > 0 ? cdnrPayload : null,
            cdnur = cdnurPayload.Length > 0 ? cdnurPayload : null,
            hsn = summary.HsnSummaries.Count > 0 ? hsnPayload : null,
            doc_issue = summary.DocumentsIssued.DocumentDetails.Count > 0 ? docPayload : null
        };

        var json = JsonSerializer.Serialize(portalJson, JsonOptions);
        var fileName = $"GSTR1_{summary.Gstin}_{summary.ReturnPeriod}.json";

        return new Gstr1JsonExportResult(fileName, "application/json", json);
    }
}

