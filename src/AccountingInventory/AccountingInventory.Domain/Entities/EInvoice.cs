using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class EInvoice : AggregateRoot
{
    public Guid SalesInvoiceId { get; private set; }
    public string Irn { get; private set; } = null!; // 64-character SHA-256 hash
    public string AckNo { get; private set; } = null!; // 15-digit acknowledgement number
    public DateTimeOffset AckDate { get; private set; }
    public string SignedQrCode { get; private set; } = null!; // Signed QR code content string
    public string? SignedInvoice { get; private set; } // Base64 encoded signed JWT
    public string Status { get; private set; } = "Generated"; // Generated, Cancelled, Failed
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelDate { get; private set; }
    public string PayloadJson { get; private set; } = null!;

    public static EInvoice Create(
        Guid salesInvoiceId,
        string irn,
        string ackNo,
        DateTimeOffset ackDate,
        string signedQrCode,
        string? signedInvoice = null,
        string payloadJson = "")
    {
        if (salesInvoiceId == Guid.Empty) throw new ArgumentException("Sales invoice ID is required.");
        if (string.IsNullOrWhiteSpace(irn) || irn.Trim().Length != 64)
            throw new ArgumentException("A valid 64-character IRN hash is required.");
        if (string.IsNullOrWhiteSpace(ackNo)) throw new ArgumentException("Acknowledgement number is required.");
        if (string.IsNullOrWhiteSpace(signedQrCode)) throw new ArgumentException("Signed QR Code is required.");

        return new EInvoice
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            SalesInvoiceId = salesInvoiceId,
            Irn = irn.Trim().ToLowerInvariant(),
            AckNo = ackNo.Trim(),
            AckDate = ackDate,
            SignedQrCode = signedQrCode.Trim(),
            SignedInvoice = signedInvoice?.Trim(),
            Status = "Generated",
            PayloadJson = payloadJson
        };
    }

    public void Cancel(string reason)
    {
        if (Status == "Cancelled") throw new InvalidOperationException("E-Invoice is already cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.");

        Status = "Cancelled";
        CancelReason = reason.Trim();
        CancelDate = DateTimeOffset.UtcNow;
    }

    private EInvoice() { }
}

