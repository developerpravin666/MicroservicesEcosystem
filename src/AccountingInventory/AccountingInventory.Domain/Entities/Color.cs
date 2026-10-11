using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// A database-managed color, scoped to one tenant. This is the ENTIRE mechanism behind
/// "which vendors can access which screen/controller": a Vendor simply owns a list of
/// permission codes (e.g. "Inventory.StockItems.Create"), editable at any time via
/// VendorEndpoints without touching code or redeploying anything. Every user assigned
/// this color picks up its current permission codes as JWT claims the next time they
/// log in or refresh their token (see LoginCommandHandler / RefreshTokenCommandHandler).
/// </summary>
public sealed class Color : AggregateRoot
{
    // Properties changed from 'init' to 'private set' to support mutations via domain methods
    //public Guid VendorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; } = null!;

    public static Color Create(string name, string? description = null)
    {
        //ValidateInput(name, description);

        return new Color
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            //VendorId = vendorId,
            Name = name.Trim(),
            Description = description?.Trim(),
        };
    }
    /// <summary>
    /// Updates the color's core details with validation constraints.
    /// </summary>
    public static Color Update(Guid id, string name, string? description, bool isActive)
    {
        //ValidateInput(name, description);

        return new Color
        {
            Id = id,
            ////VendorId = vendorId,
            IsActive = isActive,
            Name = name.Trim(),
            Description = description?.Trim()
        };
    }

    /// <summary>
    /// Soft deletes the color.
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
            throw new AccountingInventoryDomainException("Cannot activate a deleted color.");
        }
        IsActive = true;
    }

    public void InActivate()
    {
        if (IsDeleted) return;
        IsActive = false;
    }

    // Shared input validation helper used by both Create and Update
    private static void ValidateInput(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AccountingInventoryDomainException("Color name cannot be empty.");
        }
    }

    private Color() { }
}

