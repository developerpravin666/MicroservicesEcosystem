using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

public sealed class FinanceVendor : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Code { get; private set; } = null!;
    public string? Mobile { get; private set; } = null!;
    public string? Email { get; private set; } = null!;
    public string? ContactName { get; private set; } = null!;
    public string? ContactMobile { get; private set; } = null!;
    public string? Description { get; private set; }

    public static FinanceVendor Create(string name, string code, string mobile, string email,
        string contactName, string contactMobile, string? description = null)
    {
        //ValidateInput(name, code, mobile, email, contactName, contactMobile);
        return new FinanceVendor
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim(),
            Mobile = mobile.Trim(),
            Email = email.Trim(),
            ContactName = contactName.Trim(),
            ContactMobile = contactMobile.Trim(),
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(string name, string code, string mobile, string email, string contactName,
        string contactMobile, string? description, bool isActive)
    {
        //ValidateInput(name, code, mobile, email, contactName, contactMobile);
        Name = name.Trim();
        Code = code.Trim();
        Mobile = mobile.Trim();
        Email = email.Trim();
        ContactName = contactName.Trim();
        ContactMobile = contactMobile.Trim();
        Description = description?.Trim();
        IsActive = isActive;
    }

    public void Delete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
    }

    // private static void ValidateInput(string name, string code, string mobile, string email,
    //     string contactName, string contactMobile)
    // {
    //     if (string.IsNullOrWhiteSpace(name)) throw new AccountingInventoryDomainException("Finance vendor name cannot be empty.");
    //     if (string.IsNullOrWhiteSpace(code)) throw new AccountingInventoryDomainException("Finance vendor code cannot be empty.");
    //     if (string.IsNullOrWhiteSpace(mobile)) throw new AccountingInventoryDomainException("Finance vendor mobile cannot be empty.");
    //     //if (string.IsNullOrWhiteSpace(email)) throw new AccountingInventoryDomainException("Finance vendor email cannot be empty.");
    //     if (string.IsNullOrWhiteSpace(contactName)) throw new AccountingInventoryDomainException("Finance vendor contact name cannot be empty.");
    //     if (string.IsNullOrWhiteSpace(contactMobile)) throw new AccountingInventoryDomainException("Finance vendor contact mobile cannot be empty.");
    // }

    private FinanceVendor() { }
}
