using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr3bSummary;

public sealed record GetGstr3bSummaryQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<Gstr3bSummaryDto>;

public sealed record Gstr3bSummaryDto(
    string Gstin,
    string LegalName,
    string ReturnPeriod,
    DateOnly FromDate,
    DateOnly ToDate,
    Gstr3bTable31OutwardSuppliesDto OutwardSupplies,
    Gstr3bTable4EligibleItcDto EligibleItc,
    Gstr3bTable5InwardSuppliesDto InwardSupplies,
    Gstr3bTaxPaymentSummaryDto TaxPayment
);

public sealed record Gstr3bSupplyRowDto(
    string NatureOfSupply,
    decimal TotalTaxableValue,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess = 0m
);

public sealed record Gstr3bTable31OutwardSuppliesDto(
    Gstr3bSupplyRowDto OutwardTaxableSupplies,
    Gstr3bSupplyRowDto OutwardZeroRatedSupplies,
    Gstr3bSupplyRowDto OtherOutwardNilExemptedSupplies,
    Gstr3bSupplyRowDto InwardLiableToReverseCharge,
    Gstr3bSupplyRowDto NonGstOutwardSupplies
);

public sealed record Gstr3bItcRowDto(
    string Details,
    decimal IntegratedTax,
    decimal CentralTax,
    decimal StateTax,
    decimal Cess = 0m
);

public sealed record Gstr3bTable4EligibleItcDto(
    IReadOnlyList<Gstr3bItcRowDto> ItcAvailable,
    IReadOnlyList<Gstr3bItcRowDto> ItcReversed,
    Gstr3bItcRowDto NetItcAvailable
);

public sealed record Gstr3bTable5InwardSuppliesDto(
    decimal InterStateSupplies,
    decimal IntraStateSupplies
);

public sealed record Gstr3bTaxPaymentRowDto(
    string TaxType,
    decimal TotalTaxPayable,
    decimal PaidThroughItc,
    decimal TaxPaidCash,
    decimal InterestPaid = 0m,
    decimal LateFeePaid = 0m
);

public sealed record Gstr3bTaxPaymentSummaryDto(
    IReadOnlyList<Gstr3bTaxPaymentRowDto> Rows,
    decimal TotalTaxLiability,
    decimal TotalItcUtilized,
    decimal NetCashPayable
);

