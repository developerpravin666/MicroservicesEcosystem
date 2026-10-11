using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>A payment received against one sold product (sales bill).</summary>
public sealed class SalesInvoiceReceipt : AggregateRoot
{
    public Guid InvoiceId { get; private set; }
    public Guid? AdvanceId { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public DateOnly PaymentDate { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Note { get; private set; }

    public static SalesInvoiceReceipt Create(Guid invoiceId, decimal amount, string paymentMode,
        DateOnly paymentDate, string? referenceNumber, string? note, Guid? advanceId = null)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("Sales bill is required.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        InvoicePaymentRules.Validate(amount, paymentMode, paymentDate, referenceNumber, note, advanceId.HasValue);
        return new SalesInvoiceReceipt
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            InvoiceId = invoiceId,
            AdvanceId = advanceId,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            PaymentMode = paymentMode,
            PaymentDate = paymentDate,
            ReferenceNumber = Normalize(referenceNumber),
            Note = Normalize(note),
        };
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private SalesInvoiceReceipt() { }
}
