using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class Godown : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Address { get; private set; }
    public bool IsPrimary { get; private set; }

    public static Godown Create(string name, string code, string? address = null, bool isPrimary = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Godown name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Godown code is required.", nameof(code));

        return new Godown
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            IsPrimary = isPrimary,
            IsActive = true
        };
    }

    public void UpdateDetails(string name, string code, string? address, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Godown name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Godown code is required.", nameof(code));

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        IsPrimary = isPrimary;
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    private Godown() { }
}

