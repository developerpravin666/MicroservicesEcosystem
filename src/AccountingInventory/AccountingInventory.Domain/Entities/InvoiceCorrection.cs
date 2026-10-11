using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>An immutable full-unit credit/debit note; refunds are separate postings.</summary>
public sealed class InvoiceCorrection : AggregateRoot
{
    public string Kind { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public Guid PartyId { get; private set; }
    public string BillNumber { get; private set; } = null!;
    public string NoteNumber { get; private set; } = null!;
    public DateOnly NoteDate { get; private set; }
    public string Reason { get; private set; } = null!;
    public string Disposition { get; private set; } = null!;
    public decimal TaxableAmount { get; private set; }
    public decimal CgstRate { get; private set; }
    public decimal SgstRate { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    public static InvoiceCorrection Create(string kind, Guid sourceId, Guid partyId, string bill,
        DateOnly date, string reason, string disposition, decimal taxable, decimal cgstRate,
        decimal sgstRate, decimal cgst, decimal sgst, decimal total, decimal igst = 0m)
    {
        if (kind is not ("Sale" or "Purchase") || sourceId == Guid.Empty || partyId == Guid.Empty || date == default)
            throw new ArgumentException("A valid source, party and note date are required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ArgumentException("Provide a reason of at most 500 characters.");
        if (disposition is not ("Restock" or "WriteOff" or "Supplier"))
            throw new ArgumentException("Invalid stock disposition.");
        return new() { Id = Guid.NewGuid(), IsActive = true, Kind = kind, SourceId = sourceId,
            PartyId = partyId, BillNumber = bill, NoteNumber = $"{(kind == "Sale" ? "CN" : "DN")}-{date:yyyyMMdd}-{Guid.NewGuid():N}",
            NoteDate = date, Reason = reason.Trim(), Disposition = disposition, TaxableAmount = taxable,
            CgstRate = cgstRate, SgstRate = sgstRate, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst, TotalAmount = total };
    }
}

public sealed class CorrectionRefund : AggregateRoot
{
    public Guid CorrectionId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public static CorrectionRefund Create(Guid correction, DateOnly date, decimal amount, string mode, string? reference)
    {
        if (date == default || amount <= 0 || decimal.Round(amount, 2) != amount || mode is not ("Cash" or "Bank"))
            throw new ArgumentException("A positive refund with at most two decimals, date and Cash/Bank mode are required.");
        if ((reference?.Length ?? 0) > 100) throw new ArgumentException("Refund reference is too long.");
        return new() { Id = Guid.NewGuid(), IsActive = true, CorrectionId = correction, PaymentDate = date,
            Amount = amount, PaymentMode = mode, Reference = reference?.Trim() ?? "" };
    }
}

public sealed class InvoiceSnapshot : AggregateRoot
{
    public string Kind { get; private set; } = null!;
    public Guid SourceId { get; private set; }
    public string PartyName { get; private set; } = null!;
    public string PartyMobile { get; private set; } = null!;
    public string PartyAddress { get; private set; } = null!;
    public string? PartyEmail { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string SerialNumber { get; private set; } = null!;
    public string DetailsJson { get; private set; } = null!;
    public bool Reconstructed { get; private set; }
    public static InvoiceSnapshot Capture(string kind, Guid sourceId, string name, string mobile,
        string address, string? email, string product, string serial, string details, bool reconstructed = false)
        => new() { Id = Guid.NewGuid(), IsActive = true, Kind = kind, SourceId = sourceId, PartyName = name,
            PartyMobile = mobile, PartyAddress = address, PartyEmail = email, ProductName = product,
            SerialNumber = serial, DetailsJson = details, Reconstructed = reconstructed };
}

public sealed class OpeningSubledgerBalance : AggregateRoot
{
    public Guid JournalEntryId { get; private set; }
    public string Kind { get; private set; } = null!;
    public Guid PartyId { get; private set; }
    public DateOnly CutoverDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Reference { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public static OpeningSubledgerBalance Create(Guid journal, string kind, Guid party, DateOnly date,
        DateOnly due, string reference, decimal amount)
    {
        if (kind is not ("Customer" or "Vendor") || party == Guid.Empty || date == default || due == default
            || amount <= 0 || amount != decimal.Round(amount, 2) || string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
            throw new ArgumentException("Opening items require Customer/Vendor, party, dates, reference and a positive two-decimal amount.");
        return new() { Id = Guid.NewGuid(), IsActive = true, JournalEntryId = journal, Kind = kind,
            PartyId = party, CutoverDate = date, DueDate = due, Reference = reference.Trim(), Amount = amount };
    }
}

public sealed class OpeningSettlement : AggregateRoot
{
    public Guid OpeningBalanceId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMode { get; private set; } = null!;
    public static OpeningSettlement Create(Guid opening, DateOnly date, decimal amount, string mode)
    {
        if (date == default || amount <= 0 || decimal.Round(amount, 2) != amount || mode is not ("Cash" or "Bank"))
            throw new ArgumentException("Invalid opening balance settlement.");
        return new() { Id = Guid.NewGuid(), IsActive = true, OpeningBalanceId = opening, PaymentDate = date, Amount = amount, PaymentMode = mode };
    }
}
