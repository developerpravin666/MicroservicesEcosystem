using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Domain.Common;

public static class EInvoiceHelper
{
    public static string ComputeIrn(string supplierGstin, DateOnly invoiceDate, string docType, string docNumber)
    {
        var finYear = GetFinancialYear(invoiceDate);
        var normalizedDocType = docType.Trim().ToUpperInvariant();
        var normalizedDocNo = docNumber.Trim();
        var rawKey = $"{supplierGstin.Trim().ToUpperInvariant()}{finYear}{normalizedDocType}{normalizedDocNo}";

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        var sb = new StringBuilder(64);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    public static string GetFinancialYear(DateOnly date)
    {
        var year = date.Year;
        return date.Month >= 4
            ? $"{year}-{(year + 1) % 100:D2}"
            : $"{year - 1}-{year % 100:D2}";
    }

    public static string GenerateSignedQrPayload(
        string supplierGstin,
        string? buyerGstin,
        string docNo,
        DateOnly docDate,
        decimal totalAmount,
        int itemCount,
        string? mainHsn,
        string irn)
    {
        var qrData = new
        {
            sellerGstin = supplierGstin,
            buyerGstin = buyerGstin ?? "URP",
            docNo,
            docDate = docDate.ToString("dd/MM/yyyy"),
            totalValue = totalAmount,
            itemCount,
            mainHsn = mainHsn ?? "85171200",
            irn
        };
        return JsonSerializer.Serialize(qrData);
    }

    public static string GenerateEInvoiceJson(
        SalesInvoice invoice,
        CustomerBillSettings seller,
        Customer customer)
    {
        var sellerGstin = seller.TaxRegistrationNumber ?? "29AABCU9603R1ZJ";
        var buyerGstin = !string.IsNullOrWhiteSpace(invoice.CustomerGstin)
            ? invoice.CustomerGstin
            : (!string.IsNullOrWhiteSpace(customer.Gstin) ? customer.Gstin : "URP");

        var sellerStateCode = int.TryParse(seller.StateCode, out var sc) ? sc : 29;
        var posCode = int.TryParse(invoice.PlaceOfSupplyStateCode, out var pc) ? pc : sellerStateCode;

        var items = invoice.Lines.Select((l, idx) => new
        {
            itemNo = (idx + 1).ToString(),
            prdDesc = l.ItemDescription,
            isServc = l.ItemType == InvoiceItemType.Service ? "Y" : "N",
            hsnCd = !string.IsNullOrWhiteSpace(l.HsnSac) ? l.HsnSac : "85171200",
            qty = l.Quantity,
            unit = l.UnitOfMeasure ?? "NOS",
            unitPrice = l.UnitPrice,
            totAmt = decimal.Round(l.Quantity * l.UnitPrice, 2),
            discount = l.Discount,
            assAmt = l.TaxableAmount,
            gstRt = l.IgstRate > 0 ? l.IgstRate : (l.CgstRate + l.SgstRate),
            igstAmt = l.IgstAmount,
            cgstAmt = l.CgstAmount,
            sgstAmt = l.SgstAmount,
            totItemVal = l.TotalAmount
        }).ToList();

        var payload = new
        {
            Version = "1.1",
            TranDtls = new
            {
                TaxSch = "GST",
                SupTyp = buyerGstin == "URP" ? "B2C" : "B2B",
                RegRev = "N",
                IgstOnIntra = "N"
            },
            DocDtls = new
            {
                Typ = "INV",
                No = invoice.BillNumber,
                Dt = invoice.InvoiceDate.ToString("dd/MM/yyyy")
            },
            SellerDtls = new
            {
                Gstin = sellerGstin,
                LglNm = seller.CompanyName,
                TrdNm = seller.CompanyName,
                Addr1 = !string.IsNullOrWhiteSpace(seller.CompanyAddress) ? seller.CompanyAddress : "Industrial Estate",
                Loc = seller.StateName ?? "Main City",
                Pin = 560001,
                Stcd = sellerStateCode.ToString("D2"),
                Ph = seller.CompanyMobile,
                Em = seller.CompanyEmail
            },
            BuyerDtls = new
            {
                Gstin = buyerGstin,
                LglNm = customer.Name,
                TrdNm = customer.Name,
                Pos = posCode.ToString("D2"),
                Addr1 = !string.IsNullOrWhiteSpace(customer.Address) ? customer.Address : "Commercial Complex",
                Loc = customer.StateName ?? "Market",
                Pin = 560001,
                Stcd = posCode.ToString("D2"),
                Ph = customer.Mobile,
                Em = customer.Email
            },
            ItemList = items,
            ValDtls = new
            {
                AssVal = invoice.TaxableAmount,
                CgstVal = invoice.CgstAmount,
                SgstVal = invoice.SgstAmount,
                IgstVal = invoice.IgstAmount,
                CesVal = 0m,
                StCesVal = 0m,
                Discount = invoice.Discount,
                OthChrg = 0m,
                RndOffAmt = 0m,
                TotInvVal = invoice.TotalAmount
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

