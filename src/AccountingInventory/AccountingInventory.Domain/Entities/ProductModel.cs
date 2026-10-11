using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// A database-managed product model, scoped to one tenant. This is the ENTIRE mechanism behind
/// "which vendors can access which screen/controller": a Vendor simply owns a list of
/// permission codes (e.g. "Inventory.StockItems.Create"), editable at any time via
/// VendorEndpoints without touching code or redeploying anything. Every user assigned
/// this product model picks up its current permission codes as JWT claims the next time they
/// log in or refresh their token (see LoginCommandHandler / RefreshTokenCommandHandler).
/// </summary>
public sealed class ProductModel : AggregateRoot
{
    public Guid BrandId { get; private set; }
    public Guid ProductTypeId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; } = null!;

    public static ProductModel Create(Guid brandId, Guid productTypeId,string code, string name, string? description = null)
    {
        //ValidateInput(brandId, name, description);

        return new ProductModel
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            BrandId = brandId,
            ProductTypeId = productTypeId,
            Name = name.Trim(),
            Code = code.Trim(),
            Description = description?.Trim(),
        };
    }
    /// <summary>
    /// Updates the product model's core details with validation constraints.
    /// </summary>
    public static ProductModel Update(Guid id, Guid brandId, Guid productTypeId, string code, string name, string? description, bool isActive)
    {
        //ValidateInput(brandId, name, description);

        return new ProductModel
        {
            Id = id,
            BrandId = brandId,
            ProductTypeId = productTypeId,
            IsActive = isActive,
            Name = name.Trim(),
            Code = code.Trim(),
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

    private ProductModel() { }
}

