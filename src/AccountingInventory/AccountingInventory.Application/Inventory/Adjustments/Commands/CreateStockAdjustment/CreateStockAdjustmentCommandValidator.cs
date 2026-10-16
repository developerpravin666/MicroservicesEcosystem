using FluentValidation;

namespace AccountingInventory.Application.Inventory.Adjustments.Commands.CreateStockAdjustment;

public sealed class CreateStockAdjustmentCommandValidator : AbstractValidator<CreateStockAdjustmentCommand>
{
    public CreateStockAdjustmentCommandValidator()
    {
        RuleFor(x => x.AdjustmentDate).NotEmpty().WithMessage("Adjustment date is required.");
        RuleFor(x => x.GodownId).NotEmpty().WithMessage("Godown is required.");
        RuleFor(x => x.AdjustmentType).NotEmpty()
            .Must(t => t == "Shortage" || t == "Surplus")
            .WithMessage("Adjustment type must be Shortage or Surplus.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300).WithMessage("Reason is required (max 300 characters).");
        RuleFor(x => x.ItemType).NotEmpty()
            .Must(t => t == "Serialized" || t == "Sku")
            .WithMessage("Item type must be Serialized or Sku.");
        RuleFor(x => x.ItemName).NotEmpty().WithMessage("Item name is required.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        RuleFor(x => x.PhysicalQuantity).GreaterThanOrEqualTo(0).WithMessage("Physical quantity cannot be negative.");
    }
}

