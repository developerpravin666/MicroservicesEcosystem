using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class PurchasePayment : AggregateRoot
{
    public Guid VendorId { get; private set; }
    public string BillNumber { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public DateOnly PaymentDate { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Note { get; private set; }

    public static PurchasePayment Create(Guid vendorId, string billNumber, decimal amount,
        string paymentMode, DateOnly paymentDate, string? referenceNumber, string? note)
    {
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor is required.");
        if (string.IsNullOrWhiteSpace(billNumber)) throw new ArgumentException("Bill number is required.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");

        return new PurchasePayment
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            VendorId = vendorId,
            BillNumber = billNumber.Trim(),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            PaymentMode = paymentMode,
            PaymentDate = paymentDate,
            ReferenceNumber = Normalize(referenceNumber),
            Note = Normalize(note)
        };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private PurchasePayment() { }
}
