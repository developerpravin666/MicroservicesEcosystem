using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// A database-managed vendor, scoped to one tenant. This is the ENTIRE mechanism behind
/// "which vendors can access which screen/controller": a Vendor simply owns a list of
/// permission codes (e.g. "Inventory.StockItems.Create"), editable at any time via
/// VendorEndpoints without touching code or redeploying anything. Every user assigned
/// this vendor picks up its current permission codes as JWT claims the next time they
/// log in or refresh their token (see LoginCommandHandler / RefreshTokenCommandHandler).
/// </summary>
public sealed class Vendor : AggregateRoot
{
    // Properties changed from 'init' to 'private set' to support mutations via domain methods
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Mobile { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Address { get; private set; }

    public static Vendor Create(string name, string code, string mobile, string email,
        string? description = null, string? address = null)
    {
        ValidateInput(name, code, mobile, email);

        return new Vendor
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim(),
            Mobile = mobile.Trim(),
            Email = email.Trim(),
            IsActive = true,
            Description = description?.Trim(),
            Address = address?.Trim()
        };
    }

    /// <summary>
    /// Updates the vendor's core details with validation constraints.
    /// </summary>
    public static Vendor Update(Guid id, string name, string code, string mobile, string email, bool isActive,
        string? description = null, string? address = null)
    {
        //if (IsDeleted)
        //{
        //    throw new AccountingInventoryDomainException("Cannot update a deleted vendor.");
        //}

        ValidateInput(name, code, mobile, email);

        return new Vendor
        {
            Id = id,
            Name = name.Trim(),
            Code = code.Trim(),
            Mobile = mobile.Trim(),
            Email = email.Trim(),
            IsActive = isActive,
            Description = description?.Trim(),
            Address = address?.Trim()
        };
        // If your AggregateRoot base class has a tracked modified date, update it here
        // UpdatedAt = DateTime.UtcNow; 
    }

    /// <summary>
    /// Soft deletes the vendor.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted) return; // Idempotent check

        IsDeleted = true;
        IsActive = false; // Usually, deleting should also deactivate the entity
    }

    public void Activate()
    {
        if (IsDeleted)
        {
            throw new AccountingInventoryDomainException("Cannot activate a deleted vendor.");
        }
        IsActive = true;
    }

    public void InActivate()
    {
        if (IsDeleted) return;
        IsActive = false;
    }

    // Shared input validation helper used by both Create and Update
    private static void ValidateInput(string name, string code, string mobile, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AccountingInventoryDomainException("Vendor name cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AccountingInventoryDomainException("Vendor code cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new AccountingInventoryDomainException("Vendor mobile cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AccountingInventoryDomainException("Vendor email cannot be empty.");
        }
    }

    private Vendor() { }
}

