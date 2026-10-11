using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>One configurable sales bill template per tenant schema.</summary>
public sealed class CustomerBillSettings : AggregateRoot
{
    public static readonly Guid TenantSettingsId = Guid.Parse("7d27f8c1-7859-4d4c-8a9e-000000000001");

    public string CompanyName { get; private set; } = string.Empty;
    public string CompanyAddress { get; private set; } = string.Empty;
    public string CompanyMobile { get; private set; } = string.Empty;
    public string? CompanyEmail { get; private set; }
    public string? TaxRegistrationNumber { get; private set; }
    public string? StateCode { get; private set; }
    public string? StateName { get; private set; }
    public string BillTitle { get; private set; } = "SALES INVOICE";
    public string FooterNote { get; private set; } = "Thank you for your business.";
    public string PaperSize { get; private set; } = "A4";
    public bool ShowCustomerEmail { get; private set; } = true;
    public bool ShowSerialNumber { get; private set; } = true;
    public bool ShowDiscount { get; private set; } = true;
    public bool ShowPaymentHistory { get; private set; } = true;
    public bool ShowBalanceDue { get; private set; } = true;

    public static CustomerBillSettings Create(string companyName, string companyAddress, string companyMobile,
        string? companyEmail, string? taxRegistrationNumber, string billTitle, string footerNote, string paperSize,
        bool showCustomerEmail, bool showSerialNumber, bool showDiscount, bool showPaymentHistory, bool showBalanceDue,
        string? stateCode = null, string? stateName = null)
    {
        var settings = new CustomerBillSettings { Id = TenantSettingsId, IsActive = true };
        settings.Update(companyName, companyAddress, companyMobile, companyEmail, taxRegistrationNumber,
            billTitle, footerNote, paperSize, showCustomerEmail, showSerialNumber, showDiscount,
            showPaymentHistory, showBalanceDue, stateCode, stateName);
        return settings;
    }

    public void Update(string companyName, string companyAddress, string companyMobile,
        string? companyEmail, string? taxRegistrationNumber, string billTitle, string footerNote, string paperSize,
        bool showCustomerEmail, bool showSerialNumber, bool showDiscount, bool showPaymentHistory, bool showBalanceDue,
        string? stateCode = null, string? stateName = null)
    {
        CompanyName = companyName.Trim();
        CompanyAddress = companyAddress.Trim();
        CompanyMobile = companyMobile.Trim();
        CompanyEmail = Normalize(companyEmail);
        TaxRegistrationNumber = Normalize(taxRegistrationNumber);
        StateCode = GstStates.ExtractStateCode(stateCode) ?? GstStates.ExtractStateCode(TaxRegistrationNumber) ?? GstStates.ExtractStateCode(CompanyAddress);
        StateName = !string.IsNullOrWhiteSpace(stateName) ? stateName.Trim() : GstStates.GetStateName(StateCode);
        BillTitle = billTitle.Trim();
        FooterNote = footerNote.Trim();
        PaperSize = paperSize.Trim();
        ShowCustomerEmail = showCustomerEmail;
        ShowSerialNumber = showSerialNumber;
        ShowDiscount = showDiscount;
        ShowPaymentHistory = showPaymentHistory;
        ShowBalanceDue = showBalanceDue;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private CustomerBillSettings() { }
}
