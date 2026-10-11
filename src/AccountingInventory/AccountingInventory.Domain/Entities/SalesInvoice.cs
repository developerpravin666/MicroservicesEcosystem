using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum InvoiceItemType
{
    SerializedProduct = 0,
    StandardProduct = 1,
    Service = 2
}

/// <summary>Multi-line sales invoice representing a commercial sale of serialized and/or standard products to a customer.</summary>
public sealed class SalesInvoice : AggregateRoot
{
    public string BillNumber { get; private set; } = null!;
    public Guid CustomerId { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? Notes { get; private set; }
    public GstSupplyType SupplyType { get; private set; } = GstSupplyType.IntraState;
    public string? PlaceOfSupplyStateCode { get; private set; }
    public string? PlaceOfSupplyStateName { get; private set; }
    public string? CustomerGstin { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal Discount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Balance { get; private set; }
    public string PaymentStatus { get; private set; } = "Unpaid";
    public bool IsCancelled { get; private set; }

    public List<SalesInvoiceLine> Lines { get; private set; } = [];

    public static SalesInvoice Create(
        string billNumber,
        Guid customerId,
        DateOnly invoiceDate,
        int paymentTermsDays,
        string? notes,
        IEnumerable<SalesInvoiceLineDraft> lineDrafts,
        GstSupplyType supplyType = GstSupplyType.IntraState,
        string? placeOfSupplyStateCode = null,
        string? placeOfSupplyStateName = null,
        string? customerGstin = null)
    {
        if (string.IsNullOrWhiteSpace(billNumber)) throw new ArgumentException("Bill number is required.");
        if (customerId == Guid.Empty) throw new ArgumentException("Customer is required.");
        if (invoiceDate == default) throw new ArgumentException("Invoice date is required.");
        if (paymentTermsDays < 0 || paymentTermsDays > 3650) throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "Payment terms must be between 0 and 3650 days.");

        var draftList = lineDrafts.ToList();
        if (draftList.Count == 0) throw new ArgumentException("At least one invoice line item is required.");

        var invoice = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            BillNumber = billNumber.Trim(),
            CustomerId = customerId,
            InvoiceDate = invoiceDate,
            PaymentTermsDays = paymentTermsDays,
            DueDate = invoiceDate.AddDays(paymentTermsDays),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            SupplyType = supplyType,
            PlaceOfSupplyStateCode = GstStates.ExtractStateCode(placeOfSupplyStateCode),
            PlaceOfSupplyStateName = !string.IsNullOrWhiteSpace(placeOfSupplyStateName)
                ? placeOfSupplyStateName.Trim()
                : GstStates.GetStateName(placeOfSupplyStateCode),
            CustomerGstin = string.IsNullOrWhiteSpace(customerGstin) ? null : customerGstin.Trim(),
            IsCancelled = false
        };

        var lineNumber = 1;
        decimal subTotal = 0;
        decimal discountTotal = 0;
        decimal taxableTotal = 0;
        decimal cgstTotal = 0;
        decimal sgstTotal = 0;
        decimal igstTotal = 0;
        decimal grandTotal = 0;

        foreach (var draft in draftList)
        {
            var line = SalesInvoiceLine.Create(
                invoice.Id,
                lineNumber++,
                draft.ItemType,
                draft.ProductId,
                draft.ItemDescription,
                draft.SerialNumber,
                draft.SerialNumber1,
                draft.Quantity,
                draft.UnitPrice,
                draft.Discount,
                draft.TaxId,
                draft.CgstRate,
                draft.SgstRate,
                draft.IgstRate, draft.HsnSac, draft.UnitOfMeasure, draft.SkuId);

            invoice.Lines.Add(line);
            subTotal += line.TaxableAmount + line.Discount;
            discountTotal += line.Discount;
            taxableTotal += line.TaxableAmount;
            cgstTotal += line.CgstAmount;
            sgstTotal += line.SgstAmount;
            igstTotal += line.IgstAmount;
            grandTotal += line.TotalAmount;
        }

        invoice.SubTotal = decimal.Round(subTotal, 2, MidpointRounding.AwayFromZero);
        invoice.Discount = decimal.Round(discountTotal, 2, MidpointRounding.AwayFromZero);
        invoice.TaxableAmount = decimal.Round(taxableTotal, 2, MidpointRounding.AwayFromZero);
        invoice.CgstAmount = decimal.Round(cgstTotal, 2, MidpointRounding.AwayFromZero);
        invoice.SgstAmount = decimal.Round(sgstTotal, 2, MidpointRounding.AwayFromZero);
        invoice.IgstAmount = decimal.Round(igstTotal, 2, MidpointRounding.AwayFromZero);
        invoice.TotalAmount = decimal.Round(grandTotal, 2, MidpointRounding.AwayFromZero);
        invoice.AmountPaid = 0m;
        invoice.Balance = invoice.TotalAmount;
        invoice.PaymentStatus = "Unpaid";

        return invoice;
    }

    public void RecordPayment(decimal paymentAmount)
    {
        if (IsCancelled) throw new InvalidOperationException("Cancelled invoices cannot receive payments.");
        if (paymentAmount != decimal.Round(paymentAmount, 2)) throw new ArgumentException("Use at most two decimal places.");
        if (paymentAmount <= 0) throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Payment amount must be greater than zero.");
        var rounded = decimal.Round(paymentAmount, 2, MidpointRounding.AwayFromZero);
        if (rounded > Balance) throw new InvalidOperationException($"Payment of {rounded} exceeds balance due of {Balance}.");

        AmountPaid = decimal.Round(AmountPaid + rounded, 2, MidpointRounding.AwayFromZero);
        Balance = decimal.Round(TotalAmount - AmountPaid, 2, MidpointRounding.AwayFromZero);
        PaymentStatus = Balance <= 0 ? "Paid" : "Partially paid";
    }

    public void Cancel()
    {
        if (IsCancelled) return;
        IsCancelled = true;
        Balance = 0m;
        PaymentStatus = "Cancelled";
    }

    private SalesInvoice() { }
}

public sealed class SalesInvoiceLine : AggregateRoot
{
    public Guid SalesInvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid? SkuId { get; private set; }
    public InvoiceItemType ItemType { get; private set; }
    public Guid? ProductId { get; private set; }
    public string ItemDescription { get; private set; } = null!;
    public string? SerialNumber { get; private set; }
    public string? SerialNumber1 { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Discount { get; private set; }
    public string? HsnSac { get; private set; }
    public string UnitOfMeasure { get; private set; } = "NOS";
    public Guid? TaxId { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal IgstRate { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    public static SalesInvoiceLine Create(
        Guid salesInvoiceId,
        int lineNumber,
        InvoiceItemType itemType,
        Guid? productId,
        string itemDescription,
        string? serialNumber,
        string? serialNumber1,
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        Guid? taxId,
        decimal cgstRate,
        decimal sgstRate,
        decimal igstRate,
        string? hsnSac = null,
        string unitOfMeasure = "NOS", Guid? skuId = null)
    {
        if (salesInvoiceId == Guid.Empty) throw new ArgumentException("Invoice ID is required.");
        if (string.IsNullOrWhiteSpace(itemDescription)) throw new ArgumentException("Item description is required.");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        if (discount < 0) throw new ArgumentOutOfRangeException(nameof(discount), "Discount cannot be negative.");

        quantity = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero);
        unitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        if (quantity <= 0 || !Enum.IsDefined(itemType)) throw new ArgumentException("Invalid item type or quantity.");
        if (itemType == InvoiceItemType.SerializedProduct && (!productId.HasValue || productId == Guid.Empty || quantity != 1))
            throw new ArgumentException("A serialized line requires one inventory product.");
        if (itemType != InvoiceItemType.SerializedProduct && productId.HasValue && productId != Guid.Empty)
            throw new ArgumentException("Only serialized lines may reference serialized inventory.");
        if (skuId.HasValue && itemType != InvoiceItemType.StandardProduct) throw new ArgumentException("Only standard lines may reference SKU stock.");
        if (cgstRate < 0 || sgstRate < 0 || igstRate < 0 || cgstRate + sgstRate + igstRate > 100)
            throw new ArgumentException("Invalid tax rates.");
        var grossAmount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var roundedDiscount = decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
        if (roundedDiscount > grossAmount) throw new ArgumentException("Discount cannot exceed line amount.");

        var taxableAmount = decimal.Round(grossAmount - roundedDiscount, 2, MidpointRounding.AwayFromZero);
        var cgstAmount = decimal.Round(taxableAmount * (cgstRate / 100m), 2, MidpointRounding.AwayFromZero);
        var sgstAmount = decimal.Round(taxableAmount * (sgstRate / 100m), 2, MidpointRounding.AwayFromZero);
        var igstAmount = decimal.Round(taxableAmount * (igstRate / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = taxableAmount + cgstAmount + sgstAmount + igstAmount;

        return new SalesInvoiceLine
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            SalesInvoiceId = salesInvoiceId,
            LineNumber = lineNumber,
            ItemType = itemType,
            ProductId = productId,
            SkuId = skuId,
            ItemDescription = itemDescription.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            SerialNumber1 = string.IsNullOrWhiteSpace(serialNumber1) ? null : serialNumber1.Trim(),
            Quantity = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero),
            UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            Discount = roundedDiscount,
            HsnSac = string.IsNullOrWhiteSpace(hsnSac) ? null : hsnSac.Trim(),
            UnitOfMeasure = unitOfMeasure.Trim(),
            TaxId = taxId == Guid.Empty ? null : taxId,
            CgstRate = cgstRate,
            SgstRate = sgstRate,
            IgstRate = igstRate,
            TaxableAmount = taxableAmount,
            CgstAmount = cgstAmount,
            SgstAmount = sgstAmount,
            IgstAmount = igstAmount,
            TotalAmount = totalAmount
        };
    }

    private SalesInvoiceLine() { }
}

public sealed record SalesInvoiceLineDraft(
    InvoiceItemType ItemType,
    Guid? ProductId,
    string ItemDescription,
    string? SerialNumber,
    string? SerialNumber1,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    Guid? TaxId,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate = 0m, string? HsnSac = null, string UnitOfMeasure = "NOS", Guid? SkuId = null);
