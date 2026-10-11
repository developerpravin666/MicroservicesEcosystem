using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// A database-managed product type, scoped to one tenant. This is the ENTIRE mechanism behind
/// "which vendors can access which screen/controller": a Vendor simply owns a list of
/// permission codes (e.g. "Inventory.StockItems.Create"), editable at any time via
/// VendorEndpoints without touching code or redeploying anything. Every user assigned
/// this product type picks up its current permission codes as JWT claims the next time they
/// log in or refresh their token (see LoginCommandHandler / RefreshTokenCommandHandler).
/// </summary>
public sealed class ProductType : AggregateRoot
{
    // Properties changed from 'init' to 'private set' to support mutations via domain methods
    //public Guid VendorId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; } = null!;

    public static ProductType Create(string name, string? description = null)
    {
        //ValidateInput(brandId, name, description);

        return new ProductType
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            Name = name.Trim(),
            Description = description?.Trim(),
        };
    }
    /// <summary>
    /// Updates the product type's core details with validation constraints.
    /// </summary>
    public static ProductType Update(Guid id, string name, string? description, bool isActive)
    {
        //ValidateInput(brandId, name, description);

        return new ProductType
        {
            Id = id,
            IsActive = isActive,
            Name = name.Trim(),
            Description = description?.Trim()
        };
    }

    /// <summary>
    /// Soft deletes the product type.
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
            throw new AccountingInventoryDomainException("Cannot activate a deleted product type.");
        }
        IsActive = true;
    }

    public void InActivate()
    {
        if (IsDeleted) return;
        IsActive = false;
    }

    private ProductType() { }
}

