using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class RcmSelfInvoice : AggregateRoot
{
    public string VoucherNumber { get; private set; } = null!; // Self-Invoice under Section 31(3)(f)
    public string PaymentVoucherNumber { get; private set; } = null!; // Payment Voucher under Section 31(3)(g)
    public DateOnly VoucherDate { get; private set; }
    public Guid VendorId { get; private set; }
    public string VendorName { get; private set; } = null!;
    public string? VendorGstin { get; private set; }
    public string Section { get; private set; } = "9(4)"; // 9(3) = Specified Services, 9(4) = Unregistered Supplier
    public string ServiceCategory { get; private set; } = "Unregistered Goods"; // GTA, Legal Services, Sponsorship, Director Remuneration, etc.
    public string? SupplierInvoiceNumber { get; private set; }
    public DateOnly? SupplierInvoiceDate { get; private set; }
    public int SupplyType { get; private set; } = 0; // 0 = Intra-State (CGST + SGST), 1 = Inter-State (IGST)
    public string PlaceOfSupplyStateCode { get; private set; } = "27";
    public string PlaceOfSupplyStateName { get; private set; } = "Maharashtra";
    public string HsnSacCode { get; private set; } = "9965";
    public string ItemDescription { get; private set; } = null!;
    public decimal TaxableAmount { get; private set; }
    public decimal GstRate { get; private set; } // e.g. 5%, 18%
    public decimal CgstRate { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalTaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; } // Taxable + Taxes
    public bool IsItcEligible { get; private set; } = true;
    public DateOnly? ItcClaimedDate { get; private set; }
    public string PaymentMode { get; private set; } = "Bank"; // Bank, Cash, Credit
    public string? PaymentReference { get; private set; } // Cheque/UTR
    public Guid? BankAccountId { get; private set; } // Link to bank ledger if paid via bank
    public Guid? JournalEntryId { get; private set; }
    public string Status { get; private set; } = "Active"; // Active, Cancelled
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelDate { get; private set; }
    public string? Notes { get; private set; }

    public static RcmSelfInvoice Create(
        DateOnly voucherDate,
        Guid vendorId,
        string vendorName,
        string? vendorGstin,
        string section,
        string serviceCategory,
        string? supplierInvoiceNumber,
        DateOnly? supplierInvoiceDate,
        int supplyType,
        string posCode,
        string posName,
        string hsnSacCode,
        string itemDescription,
        decimal taxableAmount,
        decimal gstRate,
        bool isItcEligible = true,
        string paymentMode = "Bank",
        string? paymentReference = null,
        Guid? bankAccountId = null,
        string? notes = null,
        string? customVoucherNumber = null)
    {
        if (voucherDate == default) throw new ArgumentException("Voucher date is required.");
        if (vendorId == Guid.Empty || string.IsNullOrWhiteSpace(vendorName))
            throw new ArgumentException("Vendor ID and vendor name are required.");
        if (string.IsNullOrWhiteSpace(itemDescription))
            throw new ArgumentException("Item description is required.");
        if (taxableAmount <= 0)
            throw new ArgumentException("Taxable amount must be greater than zero.");
        if (gstRate < 0)
            throw new ArgumentException("GST rate cannot be negative.");

        var cleanSection = section.Trim();
        var cleanCategory = serviceCategory.Trim();

        decimal cgstRate = 0m, sgstRate = 0m, igstRate = 0m;
        decimal cgstAmt = 0m, sgstAmt = 0m, igstAmt = 0m;

        if (supplyType == 1) // Inter-state
        {
            igstRate = gstRate;
            igstAmt = decimal.Round(taxableAmount * (igstRate / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else // Intra-state
        {
            cgstRate = gstRate / 2m;
            sgstRate = gstRate / 2m;
            cgstAmt = decimal.Round(taxableAmount * (cgstRate / 100m), 2, MidpointRounding.AwayFromZero);
            sgstAmt = decimal.Round(taxableAmount * (sgstRate / 100m), 2, MidpointRounding.AwayFromZero);
        }

        var totalTax = cgstAmt + sgstAmt + igstAmt;
        var totalAmount = taxableAmount + totalTax;

        var voucherNum = !string.IsNullOrWhiteSpace(customVoucherNumber)
            ? customVoucherNumber.Trim()
            : $"RCM-INV-{voucherDate:yyyyMM}-{Random.Shared.Next(1000, 9999)}";

        var pvNum = $"PV-{voucherDate:yyyyMM}-{Random.Shared.Next(1000, 9999)}";

        return new RcmSelfInvoice
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            VoucherNumber = voucherNum,
            PaymentVoucherNumber = pvNum,
            VoucherDate = voucherDate,
            VendorId = vendorId,
            VendorName = vendorName.Trim(),
            VendorGstin = string.IsNullOrWhiteSpace(vendorGstin) ? null : vendorGstin.Trim().ToUpperInvariant(),
            Section = cleanSection,
            ServiceCategory = cleanCategory,
            SupplierInvoiceNumber = string.IsNullOrWhiteSpace(supplierInvoiceNumber) ? null : supplierInvoiceNumber.Trim(),
            SupplierInvoiceDate = supplierInvoiceDate,
            SupplyType = supplyType,
            PlaceOfSupplyStateCode = posCode.Trim(),
            PlaceOfSupplyStateName = posName.Trim(),
            HsnSacCode = string.IsNullOrWhiteSpace(hsnSacCode) ? "9965" : hsnSacCode.Trim(),
            ItemDescription = itemDescription.Trim(),
            TaxableAmount = decimal.Round(taxableAmount, 2),
            GstRate = gstRate,
            CgstRate = cgstRate,
            SgstRate = sgstRate,
            IgstRate = igstRate,
            CgstAmount = cgstAmt,
            SgstAmount = sgstAmt,
            IgstAmount = igstAmt,
            TotalTaxAmount = totalTax,
            TotalAmount = totalAmount,
            IsItcEligible = isItcEligible,
            ItcClaimedDate = isItcEligible ? voucherDate : null,
            PaymentMode = paymentMode.Trim(),
            PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? null : paymentReference.Trim(),
            BankAccountId = bankAccountId,
            Status = "Active",
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public void SetJournalEntryId(Guid journalEntryId)
    {
        JournalEntryId = journalEntryId;
    }

    public void Cancel(string reason)
    {
        if (Status == "Cancelled") throw new InvalidOperationException("RCM Self-Invoice voucher is already cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.");

        Status = "Cancelled";
        CancelReason = reason.Trim();
        CancelDate = DateTimeOffset.UtcNow;
    }

    public void MarkItcClaimed(DateOnly date)
    {
        IsItcEligible = true;
        ItcClaimedDate = date;
    }

    private RcmSelfInvoice() { }
}

