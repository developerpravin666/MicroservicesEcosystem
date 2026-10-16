using FluentValidation;

namespace AccountingInventory.Application.Inventory.Transfers.Commands.CreateStockTransfer;

public sealed class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.TransferDate).NotEmpty().WithMessage("Transfer date is required.");
        RuleFor(x => x.FromGodownId).NotEmpty().WithMessage("Source godown is required.");
        RuleFor(x => x.ToGodownId).NotEmpty().WithMessage("Destination godown is required.");
        RuleFor(x => x).Must(x => x.FromGodownId != x.ToGodownId)
            .WithMessage("Source and destination godowns cannot be the same.");

        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line item must be transferred.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemType).NotEmpty().Must(t => t == "Serialized" || t == "Sku")
                .WithMessage("Item type must be Serialized or Sku.");
            line.RuleFor(l => l.ItemName).NotEmpty().WithMessage("Item name is required.");
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        });
    }
}

