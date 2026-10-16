using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr1Summary;

public sealed record GetGstr1SummaryQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<Gstr1SummaryDto>;

public sealed record Gstr1SummaryDto(
    string Gstin,
    string LegalName,
    string ReturnPeriod,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal GrossTurnover,
    decimal TotalTaxableValue,
    decimal TotalTax,
    decimal TotalCgst,
    decimal TotalSgst,
    decimal TotalIgst,
    decimal TotalCess,
    IReadOnlyList<Gstr1B2bInvoiceGroupDto> B2bInvoices,
    IReadOnlyList<Gstr1B2cLargeInvoiceDto> B2cLargeInvoices,
    IReadOnlyList<Gstr1B2cSmallSummaryDto> B2cSmallSummaries,
    IReadOnlyList<Gstr1CdnrInvoiceGroupDto> CreditDebitNotesRegistered,
    IReadOnlyList<Gstr1CdnumInvoiceDto> CreditDebitNotesUnregistered,
    IReadOnlyList<Gstr1HsnSummaryDto> HsnSummaries,
    Gstr1DocIssuedSummaryDto DocumentsIssued
);

public sealed record Gstr1ItemDetailDto(
    decimal Rate,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount = 0m
);

public sealed record Gstr1InvoiceLineDto(
    int LineNumber,
    Gstr1ItemDetailDto ItemDetails
);

public sealed record Gstr1B2bInvoiceItemDto(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal InvoiceValue,
    string PlaceOfSupply,
    string ReverseCharge,
    string InvoiceType,
    string? EcommerceGstin,
    IReadOnlyList<Gstr1InvoiceLineDto> Items
);

public sealed record Gstr1B2bInvoiceGroupDto(
    string CustomerGstin,
    string CustomerLegalName,
    IReadOnlyList<Gstr1B2bInvoiceItemDto> Invoices
);

public sealed record Gstr1B2cLargeInvoiceDto(
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal InvoiceValue,
    string PlaceOfSupply,
    string? EcommerceGstin,
    decimal ApplicableTaxRatePercent,
    IReadOnlyList<Gstr1InvoiceLineDto> Items
);

public sealed record Gstr1B2cSmallSummaryDto(
    string PlaceOfSupply,
    string SupplyType, // "IntraState" or "InterState"
    decimal Rate,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount = 0m,
    string? EcommerceGstin = null
);

public sealed record Gstr1CdnrItemDto(
    string NoteNumber,
    DateOnly NoteDate,
    string NoteType, // "C" (Credit) or "D" (Debit)
    string OriginalInvoiceNumber,
    DateOnly OriginalInvoiceDate,
    decimal NoteValue,
    string PlaceOfSupply,
    string ReverseCharge,
    IReadOnlyList<Gstr1InvoiceLineDto> Items
);

public sealed record Gstr1CdnrInvoiceGroupDto(
    string CustomerGstin,
    string CustomerLegalName,
    IReadOnlyList<Gstr1CdnrItemDto> Notes
);

public sealed record Gstr1CdnumInvoiceDto(
    string NoteNumber,
    DateOnly NoteDate,
    string NoteType,
    string OriginalInvoiceNumber,
    DateOnly OriginalInvoiceDate,
    decimal NoteValue,
    string PlaceOfSupply,
    IReadOnlyList<Gstr1InvoiceLineDto> Items
);

public sealed record Gstr1HsnSummaryDto(
    int SerialNumber,
    string HsnCode,
    string Description,
    string Uqc,
    decimal TotalQuantity,
    decimal TotalValue,
    decimal TaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess = 0m
);

public sealed record Gstr1DocIssuedDetailDto(
    int DocNatureNumber,
    string DocNatureDescription,
    string FromSerialNumber,
    string ToSerialNumber,
    int TotalNumber,
    int CancelledNumber,
    int NetIssuedNumber
);

public sealed record Gstr1DocIssuedSummaryDto(
    IReadOnlyList<Gstr1DocIssuedDetailDto> DocumentDetails
);

