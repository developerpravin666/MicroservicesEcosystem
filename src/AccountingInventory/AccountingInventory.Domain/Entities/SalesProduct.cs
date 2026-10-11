using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>A tenant scoped product sale linked to a customer.</summary>
public sealed class SalesProduct : AggregateRoot
{
    public string BillNumber { get; private set; } = null!;
    public string ProductId { get; private set; } = null!;
    public Guid CustomerId { get; private set; }
    public DateOnly SaleDate { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal ProductPrice { get; private set; }
    public decimal SellingPrice { get; private set; }
    public decimal Discount { get; private set; }
    public Guid? TaxId { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsReturned { get; private set; }
    public void MarkReturned() => IsReturned = true;

    public static SalesProduct Create(string billNumber, string productId,
        Guid customerId, DateOnly saleDate,
        decimal productPrice, decimal sellingPrice, decimal discount,
        Guid? taxId, decimal cgstRate, decimal sgstRate, int paymentTermsDays = 0)
    {
        var sale = new SalesProduct
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            BillNumber = billNumber.Trim(),
            ProductId = productId.Trim(),
            CustomerId = customerId,
            SaleDate = saleDate,
            PaymentTermsDays = ValidateTerms(paymentTermsDays),
            DueDate = saleDate.AddDays(paymentTermsDays),
            ProductPrice = productPrice,
            SellingPrice = sellingPrice,
            Discount = discount
        };
        sale.SetTax(taxId, cgstRate, sgstRate);
        return sale;
    }

    public void Update(string productId,
        Guid customerId, DateOnly saleDate,
        decimal productPrice, decimal sellingPrice, decimal discount,
        Guid? taxId, decimal cgstRate, decimal sgstRate, int paymentTermsDays = 0)
    {
        ProductId = productId.Trim();
        CustomerId = customerId;
        SaleDate = saleDate;
        PaymentTermsDays = ValidateTerms(paymentTermsDays);
        DueDate = saleDate.AddDays(paymentTermsDays);
        ProductPrice = productPrice;
        SellingPrice = sellingPrice;
        Discount = discount;
        SetTax(taxId, cgstRate, sgstRate);
    }

    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
    }

    private void SetTax(Guid? taxId, decimal cgstRate, decimal sgstRate)
    {
        if (cgstRate < 0 || sgstRate < 0 || cgstRate > 100 || sgstRate > 100)
            throw new ArgumentOutOfRangeException(nameof(cgstRate), "Tax rates must be between zero and 100 percent.");
        if (Discount < 0 || Discount > SellingPrice)
            throw new ArgumentOutOfRangeException(nameof(Discount), "Discount must be between zero and the selling price.");
        TaxId = taxId == Guid.Empty ? null : taxId;
        CgstRate = cgstRate;
        SgstRate = sgstRate;
        TaxableAmount = decimal.Round(SellingPrice - Discount, 2, MidpointRounding.AwayFromZero);
        CgstAmount = decimal.Round(TaxableAmount * CgstRate / 100m, 2, MidpointRounding.AwayFromZero);
        SgstAmount = decimal.Round(TaxableAmount * SgstRate / 100m, 2, MidpointRounding.AwayFromZero);
        TotalAmount = TaxableAmount + CgstAmount + SgstAmount;
    }

    private static int ValidateTerms(int days)
    {
        if (days is < 0 or > 3650) throw new ArgumentOutOfRangeException(nameof(days), "Payment terms must be between 0 and 3650 days.");
        return days;
    }

    private SalesProduct() { }
}
