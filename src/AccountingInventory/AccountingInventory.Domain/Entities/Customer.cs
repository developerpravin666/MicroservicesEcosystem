using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

/// <summary>Customer contact details used by sales in the current tenant.</summary>
public sealed class Customer : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Mobile { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? StateCode { get; private set; }
    public string? StateName { get; private set; }
    public string? Gstin { get; private set; }

    public static Customer Create(
        string name,
        string mobile,
        string address,
        string? email,
        string? stateCode = null,
        string? stateName = null,
        string? gstin = null)
    {
        var resolvedGstin = Normalize(gstin);
        var resolvedCode = GstStates.ExtractStateCode(stateCode) ?? GstStates.ExtractStateCode(resolvedGstin) ?? GstStates.ExtractStateCode(address);
        var resolvedName = !string.IsNullOrWhiteSpace(stateName) ? stateName.Trim() : GstStates.GetStateName(resolvedCode);

        return new()
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            Name = name.Trim(),
            Mobile = mobile.Trim(),
            Address = address.Trim(),
            Email = NormalizeEmail(email),
            StateCode = resolvedCode,
            StateName = resolvedName,
            Gstin = resolvedGstin
        };
    }

    public void UpdateContactDetails(
        string name,
        string mobile,
        string address,
        string? email,
        string? stateCode = null,
        string? stateName = null,
        string? gstin = null)
    {
        Name = name.Trim();
        Mobile = mobile.Trim();
        Address = address.Trim();
        Email = NormalizeEmail(email);

        var resolvedGstin = Normalize(gstin);
        var resolvedCode = GstStates.ExtractStateCode(stateCode) ?? GstStates.ExtractStateCode(resolvedGstin) ?? GstStates.ExtractStateCode(address);
        StateCode = resolvedCode ?? StateCode;
        StateName = !string.IsNullOrWhiteSpace(stateName) ? stateName.Trim() : (GstStates.GetStateName(StateCode) ?? StateName);
        Gstin = resolvedGstin ?? Gstin;
    }

    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
    }

    private static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Customer() { }
}
