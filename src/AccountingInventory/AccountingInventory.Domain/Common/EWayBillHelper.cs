using System.Text.Json;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Domain.Common;

public static class EWayBillHelper
{
    public static string GenerateEWayBillJson(
        SalesInvoice invoice,
        CustomerBillSettings seller,
        Customer customer,
        EWayBill ewb)
    {
        var sellerGstin = seller.TaxRegistrationNumber ?? "29AABCU9603R1ZJ";
        var buyerGstin = !string.IsNullOrWhiteSpace(invoice.CustomerGstin)
            ? invoice.CustomerGstin
            : (!string.IsNullOrWhiteSpace(customer.Gstin) ? customer.Gstin : "URP");

        var sellerStateCode = int.TryParse(seller.StateCode, out var sc) ? sc : 29;
        var buyerStateCode = int.TryParse(invoice.PlaceOfSupplyStateCode, out var bc) ? bc : sellerStateCode;

        var items = invoice.Lines.Select((l, idx) => new
        {
            itemNo = idx + 1,
            productName = l.ItemDescription,
            productDesc = l.ItemDescription,
            hsnCode = !string.IsNullOrWhiteSpace(l.HsnSac) ? int.TryParse(l.HsnSac, out var h) ? h : 85171200 : 85171200,
            quantity = l.Quantity,
            qtyUnit = l.UnitOfMeasure ?? "NOS",
            taxableAmount = l.TaxableAmount,
            cgstRate = l.CgstRate,
            sgstRate = l.SgstRate,
            igstRate = l.IgstRate,
            cessRate = 0m
        }).ToList();

        var payload = new
        {
            supplyType = ewb.SupplyType,
            subSupplyType = ewb.SubSupplyType,
            docType = ewb.DocType,
            docNo = invoice.BillNumber,
            docDate = invoice.InvoiceDate.ToString("dd/MM/yyyy"),
            fromGstin = sellerGstin,
            fromTrdName = seller.CompanyName,
            fromAddr1 = !string.IsNullOrWhiteSpace(seller.CompanyAddress) ? seller.CompanyAddress : "Industrial Estate",
            fromPlace = seller.StateName ?? "Main City",
            fromPincode = 560001,
            actFromStateCode = sellerStateCode,
            fromStateCode = sellerStateCode,
            toGstin = buyerGstin,
            toTrdName = customer.Name,
            toAddr1 = !string.IsNullOrWhiteSpace(customer.Address) ? customer.Address : "Commercial Complex",
            toPlace = customer.StateName ?? "Market",
            toPincode = 560001,
            actToStateCode = buyerStateCode,
            toStateCode = buyerStateCode,
            totalValue = invoice.TaxableAmount,
            cgstValue = invoice.CgstAmount,
            sgstValue = invoice.SgstAmount,
            igstValue = invoice.IgstAmount,
            cessValue = 0m,
            totInvValue = invoice.TotalAmount,
            transDistance = ewb.DistanceKm,
            transporterId = ewb.TransporterId,
            transporterName = ewb.TransporterName,
            transDocNo = ewb.TransDocNo,
            transDocDate = ewb.TransDocDate?.ToString("dd/MM/yyyy"),
            transMode = ewb.TransMode.ToString(),
            vehicleNo = ewb.VehicleNo,
            vehicleType = ewb.VehicleType,
            itemList = items
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

