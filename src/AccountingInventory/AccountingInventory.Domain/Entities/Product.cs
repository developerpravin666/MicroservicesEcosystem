using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>A product purchased from a vendor, including its identifying serials and cost.</summary>
public sealed class Product : AggregateRoot
{
    public string? HsnSac { get; private set; }
    public Guid? TaxId { get; private set; }
    public string TaxTreatment { get; private set; } = "Taxable";
    public void SetTaxDetails(Guid? taxId, string treatment) { if (!AccountingInventory.Domain.Entities.Tax.Treatments.Contains(treatment) || (treatment != "Taxable" && Cgst + Sgst != 0)) throw new ArgumentException("Invalid purchase tax treatment."); TaxId = taxId; TaxTreatment = treatment; }
    public Guid VendorId { get; private set; }
    public Guid BrandId { get; private set; }
    public Guid ProductTypeId { get; private set; }
    public Guid ProductModelId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid ColorId { get; private set; }
    public string SerialNumber { get; private set; } = null!;
    public string? SerialNumber1 { get; private set; }
    public string? BillNumber { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public DateOnly DueDate { get; private set; }
    public bool IsSold { get; private set; }
    public bool IsOpeningStock { get; private set; }
    public void StageOpeningStock() { IsOpeningStock = true; IsActive = false; }
    public void ActivateOpeningStock() { IsOpeningStock = true; IsActive = true; }
    public decimal PurchasePrice { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Cgst { get; private set; }
    public decimal Sgst { get; private set; }
    public decimal Tax { get; private set; }

    public static Product Create(Guid vendorId, Guid brandId, Guid productTypeId, Guid productModelId,
        Guid variantId, Guid colorId, string serialNumber, string? serialNumber1, string? billNumber,
        decimal purchasePrice, decimal discount, decimal cgst, decimal sgst, decimal tax, DateOnly? purchaseDate = null,
        int paymentTermsDays = 0, string? hsnSac = null)
    {
        Validate(vendorId, brandId, productTypeId, productModelId, variantId, colorId,
            serialNumber, purchasePrice, discount, cgst, sgst, tax);
        var invoiceDate = purchaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return new Product
        {
            Id = Guid.NewGuid(), IsActive = true, VendorId = vendorId, BrandId = brandId,
            ProductTypeId = productTypeId, ProductModelId = productModelId, VariantId = variantId,
            ColorId = colorId, SerialNumber = serialNumber.Trim(),
            SerialNumber1 = string.IsNullOrWhiteSpace(serialNumber1) ? null : serialNumber1.Trim(),
            BillNumber = NormalizeBillNumber(billNumber), PurchaseDate = invoiceDate,
            PaymentTermsDays = ValidateTerms(paymentTermsDays),
            DueDate = invoiceDate.AddDays(paymentTermsDays), IsSold = false,
            HsnSac = HsnSacCode.Normalize(hsnSac),
            PurchasePrice = purchasePrice, Discount = discount,
            Cgst = cgst, Sgst = sgst, Tax = tax,
            TotalAmount = CalculateTotalAmount(purchasePrice, discount, cgst, sgst)
        };
    }

    public void Update(Guid vendorId, Guid brandId, Guid productTypeId, Guid productModelId,
        Guid variantId, Guid colorId, string serialNumber, string? serialNumber1, string? billNumber,
        decimal purchasePrice, decimal discount, decimal cgst, decimal sgst, decimal tax, DateOnly? purchaseDate = null,
        int paymentTermsDays = 0, string? hsnSac = null)
    {
        Validate(vendorId, brandId, productTypeId, productModelId, variantId, colorId,
            serialNumber, purchasePrice, discount, cgst, sgst, tax);
        VendorId = vendorId; BrandId = brandId; ProductTypeId = productTypeId;
        ProductModelId = productModelId; VariantId = variantId; ColorId = colorId;
        SerialNumber = serialNumber.Trim();
        SerialNumber1 = string.IsNullOrWhiteSpace(serialNumber1) ? null : serialNumber1.Trim();
        BillNumber = NormalizeBillNumber(billNumber);
        if (purchaseDate.HasValue) PurchaseDate = purchaseDate.Value;
        PaymentTermsDays = ValidateTerms(paymentTermsDays);
        DueDate = PurchaseDate.AddDays(paymentTermsDays);
        HsnSac = HsnSacCode.Normalize(hsnSac);
        PurchasePrice = purchasePrice; Discount = discount;
        Cgst = cgst; Sgst = sgst; Tax = tax;
        TotalAmount = CalculateTotalAmount(purchasePrice, discount, cgst, sgst);
    }

    public void Delete() { IsDeleted = true; IsActive = false; }

    public void MarkSold()
    {
        if (IsSold) throw new InvalidOperationException("This product has already been sold.");
        IsSold = true;
    }

    public void MarkAvailable() => IsSold = false;

    public void WriteOff()
    {
        if (!IsActive || IsSold) throw new InvalidOperationException("Only active, unsold products can be written off.");
        IsActive = false;
    }

    public void Activate()
    {
        if (IsSold) throw new InvalidOperationException("Sold products cannot be re-activated.");
        IsActive = true;
    }

    public void SetActive(bool isActive)
    {
        if (IsSold && isActive) throw new InvalidOperationException("Sold products cannot be re-activated.");
        IsActive = isActive;
    }

    private static string? NormalizeBillNumber(string? billNumber)
        => string.IsNullOrWhiteSpace(billNumber) ? null : billNumber.Trim();

    private static int ValidateTerms(int days)
    {
        if (days is < 0 or > 3650) throw new ArgumentOutOfRangeException(nameof(days), "Payment terms must be between 0 and 3650 days.");
        return days;
    }

    private static void Validate(Guid vendorId, Guid brandId, Guid productTypeId, Guid productModelId,
        Guid variantId, Guid colorId, string serialNumber, decimal purchasePrice,
        decimal discount, decimal cgst, decimal sgst, decimal tax)
    {
        if (vendorId == Guid.Empty || brandId == Guid.Empty || productTypeId == Guid.Empty ||
            productModelId == Guid.Empty || variantId == Guid.Empty || colorId == Guid.Empty)
            throw new ArgumentException("All product reference IDs are required.");
        if (string.IsNullOrWhiteSpace(serialNumber)) throw new ArgumentException("SerialNumber is required.");
        if (purchasePrice < 0 || discount < 0 || discount > purchasePrice || cgst < 0 || sgst < 0 || tax < 0)
            throw new ArgumentException("Price, discount, and tax values cannot be negative.");
        if (decimal.Round(tax, 2) != decimal.Round(cgst + sgst, 2))
            throw new ArgumentException("Tax must equal the CGST and SGST rates combined.");
    }

    private static decimal CalculateTotalAmount(decimal purchasePrice, decimal discount, decimal cgst, decimal sgst)
    {
        var taxableAmount = purchasePrice - discount;
        return decimal.Round(taxableAmount * (1 + (cgst + sgst) / 100), 2, MidpointRounding.AwayFromZero);
    }

    private Product() { }
}
