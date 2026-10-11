namespace AccountingInventory.Domain.Entities;

public static class InvoicePaymentRules
{
    public static void Validate(decimal amount, string mode, DateOnly date, string? reference, string? note, bool advance = false)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new ArgumentException("Use a positive payment amount with at most two decimals.");
        if (date == default) throw new ArgumentException("Payment date is required.");
        if (!(mode is "Cash" or "UPI" or "Card" or "Bank" or "Cheque" or "OnlineTransfer" or "Other") && !(advance && mode == "Advance"))
            throw new ArgumentException("Choose a supported payment method.");
        if (mode is "UPI" or "Card" or "Bank" or "Cheque" or "OnlineTransfer" && string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("A transaction reference is required for this payment method.");
        if ((reference?.Length ?? 0) > 100 || (note?.Length ?? 0) > 500) throw new ArgumentException("Payment reference or note is too long.");
    }
}

public sealed class CustomerAdvance : BuildingBlocks.Domain.AggregateRoot
{
    public Guid CustomerId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public string? ReferenceNumber { get; private set; }
    public decimal Amount { get; private set; }
    public decimal RemainingAmount { get; private set; }
    public static CustomerAdvance Create(Guid customer, decimal amount, string mode, DateOnly date, string? reference)
    {
        InvoicePaymentRules.Validate(amount, mode, date, reference, null);
        return new() { Id = Guid.NewGuid(), IsActive = true, CustomerId = customer, Amount = amount, RemainingAmount = amount,
            PaymentDate = date, PaymentMode = mode, ReferenceNumber = reference };
    }
    public void Consume(decimal amount)
    {
        if (amount <= 0 || amount != decimal.Round(amount, 2) || amount > RemainingAmount) throw new ArgumentException("Amount exceeds the available advance.");
        RemainingAmount -= amount;
    }
}

public sealed class CustomerAdvanceRefund : BuildingBlocks.Domain.AggregateRoot
{
    public Guid AdvanceId { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public string? ReferenceNumber { get; private set; }
    public static CustomerAdvanceRefund Create(Guid id, decimal amount, DateOnly date, string mode, string? reference)
    {
        InvoicePaymentRules.Validate(amount, mode, date, reference, null);
        return new() { Id = Guid.NewGuid(), IsActive = true, AdvanceId = id, Amount = amount, PaymentDate = date,
            PaymentMode = mode, ReferenceNumber = reference };
    }
}
